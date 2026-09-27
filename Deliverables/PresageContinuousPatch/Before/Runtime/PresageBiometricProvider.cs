using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace TheLastWatch.Integrations
{
    public enum PresageBiometricState
    {
        Off,
        Starting,
        Measuring,
        NeedsAttention,
        Paused,
        Error
    }

    public readonly struct PresageBiometricSample
    {
        public PresageBiometricSample(double timestampSeconds, bool hasPulse, float pulseBpm,
            float pulseConfidence, bool pulseStable, bool hasBreathing, float breathingRate,
            float breathingConfidence, bool breathingStable, bool hasHrv, float hrvRmssd,
            float hrvSdnn, float hrvConfidence)
        {
            TimestampSeconds = timestampSeconds;
            HasPulse = hasPulse;
            PulseBpm = pulseBpm;
            PulseConfidence = pulseConfidence;
            PulseStable = pulseStable;
            HasBreathing = hasBreathing;
            BreathingRate = breathingRate;
            BreathingConfidence = breathingConfidence;
            BreathingStable = breathingStable;
            HasHrv = hasHrv;
            HrvRmssd = hrvRmssd;
            HrvSdnn = hrvSdnn;
            HrvConfidence = hrvConfidence;
        }

        public double TimestampSeconds { get; }
        public bool HasPulse { get; }
        public float PulseBpm { get; }
        public float PulseConfidence { get; }
        public bool PulseStable { get; }
        public bool HasBreathing { get; }
        public float BreathingRate { get; }
        public float BreathingConfidence { get; }
        public bool BreathingStable { get; }
        public bool HasHrv { get; }
        public float HrvRmssd { get; }
        public float HrvSdnn { get; }
        public float HrvConfidence { get; }
        public bool PulseIsUsable(float minimumConfidence) =>
            HasPulse && PulseStable && PulseConfidence >= minimumConfidence && PulseBpm > 0;
        public bool BreathingIsUsable(float minimumConfidence) =>
            HasBreathing && BreathingStable && BreathingConfidence >= minimumConfidence && BreathingRate > 0;
    }

    [DisallowMultipleComponent]
    public sealed class PresageBiometricProvider : MonoBehaviour
    {
        [Range(0, 8)] public int cameraDeviceIndex;
        [Range(320, 1920)] public int captureWidth = 1280;
        [Range(240, 1080)] public int captureHeight = 720;
        [Range(15, 60)] public int captureFps = 30;

        public bool HasConsent { get; private set; }
        public bool ProactiveGuidance { get; private set; }
        public PresageBiometricState State { get; private set; } = PresageBiometricState.Off;
        public string StatusText { get; private set; } = "Wellness camera off";
        public string ValidationHint { get; private set; } = string.Empty;
        public PresageBiometricSample LatestSample { get; private set; }
        public bool HasSample { get; private set; }
        public bool HasFreshSample => HasConsent && HasSample && State == PresageBiometricState.Measuring && Time.unscaledTime - lastSampleAt < 8;
        public bool IsMeasuring => State == PresageBiometricState.Measuring || State == PresageBiometricState.NeedsAttention;
        public PresageCameraCheck CameraCheck { get; } = new PresageCameraCheck();
        public Texture2D CameraPreview { get; private set; }
        private bool previewEnabled;
        private float previewAt = -1000;
        public bool HasCameraPreview => CameraPreview != null && Time.unscaledTime - previewAt < 4;
        public event Action<PresageBiometricSample> SampleUpdated;

        private readonly ConcurrentQueue<Envelope> messages = new ConcurrentQueue<Envelope>();
        private Process sidecar;
        private PresageColourCamera colourCamera;
        private int generation;
        private bool resumeAfterStop;
        private bool stopping;
        private string activeKey;
        private float lastSampleAt;

        [Serializable]
        private sealed class WireMessage
        {
            public string type;
            public int statusCode;
            public int validationCode;
            public int errorCode;
            public string hint;
            public string message;
            public bool retryable;
            public double timestampUs;
            public bool hasPulse;
            public float pulseBpm;
            public float pulseConfidence;
            public bool pulseStable;
            public bool hasBreathing;
            public float breathingRate;
            public float breathingConfidence;
            public bool breathingStable;
            public bool hasHrv;
            public float hrvRmssd;
            public float hrvSdnn;
            public float hrvConfidence;
            public int width, height;
            public string rgb;
        }

        private readonly struct Envelope
        {
            public Envelope(int generation, string payload)
            {
                Generation = generation;
                Payload = payload;
            }
            public int Generation { get; }
            public string Payload { get; }
        }

        public void SetConsent(bool enabled, bool allowProactiveGuidance)
        {
            HasConsent = enabled;
            ProactiveGuidance = enabled && allowProactiveGuidance;
            if (!enabled) StopMeasurement(true);
            else
            {
                if (State == PresageBiometricState.Off || State == PresageBiometricState.Paused || State == PresageBiometricState.Error)
                    StatusText = "Starting wellness camera…";
                // Camera consent is independent of ElevenLabs connectivity. Begin measuring now so the
                // player sees calibration feedback even while voice is still connecting or out of range.
                ResumeMeasurement();
            }
        }

        public void SetProactiveGuidance(bool enabled)
        {
            ProactiveGuidance = HasConsent && enabled;
        }

        public void ResumeMeasurement()
        {
            if (!HasConsent) return;
            if (sidecar != null && !HasExited(sidecar))
            {
                if (stopping) resumeAfterStop = true;
                return;
            }
            StartMeasurement();
        }

        public void PauseMeasurement()
        {
            resumeAfterStop = false;
            StopSidecar(PresageBiometricState.Paused, "Wellness camera paused");
        }

        public void SetPreviewEnabled(bool enabled)
        {
            previewEnabled = enabled;
            if (!enabled) ClearPreview();
            if (sidecar != null && !HasExited(sidecar) && !stopping)
            {
                try { sidecar.StandardInput.WriteLine(enabled ? "preview-on" : "preview-off"); sidecar.StandardInput.Flush(); }
                catch { }
            }
        }

        public void RetryCameraTest()
        {
            PauseMeasurement();
            ResumeMeasurement();
        }

        private void ClearPreview()
        {
            if (CameraPreview != null) Destroy(CameraPreview);
            CameraPreview = null; previewAt = -1000;
        }

        public string Badge
        {
            get
            {
                if (!HasConsent) return "Wellness camera off";
                if (HasFreshSample)
                {
                    string pulse = LatestSample.PulseIsUsable(60) ? LatestSample.PulseBpm.ToString("0", CultureInfo.InvariantCulture) + " bpm" : "pulse settling";
                    string breathing = LatestSample.BreathingIsUsable(60) ? LatestSample.BreathingRate.ToString("0", CultureInfo.InvariantCulture) + "/min" : "breathing settling";
                    return "Wellness camera · " + pulse + " · " + breathing;
                }
                return StatusText;
            }
        }

        private void StartMeasurement()
        {
            if (!Application.isPlaying || !HasConsent || sidecar != null && !HasExited(sidecar)) return;
            string script = ResolveBridgeScript();
            if (string.IsNullOrEmpty(script) || !File.Exists(script))
            {
                Fail("Presage bridge is not installed. Run the biometric bridge installer.");
                return;
            }
            string key = ReadSetting("TLW_PRESAGE_API_KEY") ?? ReadSetting("SMARTSPECTRA_API_KEY");
            if (string.IsNullOrWhiteSpace(key))
            {
                Fail("Presage key missing. Set TLW_PRESAGE_API_KEY before launching Unity.");
                return;
            }

            int epoch = ++generation;
            CameraCheck.Reset();
            activeKey = key;
            stopping = false;
            resumeAfterStop = false;
            var start = new ProcessStartInfo
            {
                FileName = ResolveNodeExecutable(),
                Arguments = Quote(script) + " --camera-index " + cameraDeviceIndex +
                    " --width " + captureWidth + " --height " + captureHeight + " --fps " + captureFps + (previewEnabled ? " --preview true" : ""),
                WorkingDirectory = Path.GetDirectoryName(script),
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            // A running Unity/Hub process can hold an old environment snapshot. Pass the
            // current saved user setting directly to the child without persisting the secret.
            start.EnvironmentVariables["TLW_PRESAGE_API_KEY"] = key;
            var process = new Process { StartInfo = start, EnableRaisingEvents = true };
            process.OutputDataReceived += (_, eventArgs) =>
            {
                // EOF follows the final JSON error; Process.Exited can race ahead of it.
                if (eventArgs.Data == null) messages.Enqueue(new Envelope(epoch, "{\"type\":\"exited\"}"));
                if (!string.IsNullOrWhiteSpace(eventArgs.Data) && messages.Count < 128) messages.Enqueue(new Envelope(epoch, eventArgs.Data));
            };
            process.ErrorDataReceived += (_, __) => { }; // Never surface native stderr; it may contain provider internals.
            try
            {
                colourCamera?.Dispose();
                colourCamera=new PresageColourCamera(cameraDeviceIndex,captureWidth,captureHeight,captureFps);
                start.Arguments+=" --frame-pipe "+Quote(colourCamera.PipeName);
                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                sidecar = process;
                State = PresageBiometricState.Starting;
                StatusText = "Starting wellness camera…";
                ValidationHint = string.Empty;
                HasSample = false;
                UnityEngine.Debug.Log("[MindSpace Presage] Bridge started; waiting for camera validation and stable measurements.", this);
            }
            catch (Exception exception)
            {
                try { if (!HasExited(process)) process.Kill(); } catch { }
                process.Dispose();
                Fail("Could not start camera / Presage. Check camera access and TLW_NODE_PATH. " + Safe(exception.Message));
            }
        }

        private void Update()
        {
            // Read SDK errors before the broken pipe they may cause, so the real
            // failure isn't replaced by a generic camera-connectivity message.
            WireMessage newestPreview = null;
            for (int i = 0; i < 64 && messages.TryDequeue(out Envelope envelope); i++)
            {
                if (envelope.Generation != generation) continue;
                WireMessage message;
                try { message = JsonUtility.FromJson<WireMessage>(envelope.Payload); }
                catch { continue; }
                if (message == null || string.IsNullOrEmpty(message.type)) continue;
                if (stopping && message.type != "exited") continue;
                switch (message.type)
                {
                    case "frame":
                        CameraCheck.Frames(Time.unscaledTime);
                        break;
                    case "preview":
                        if(colourCamera!=null)break; // Never substitute SDK-processed grayscale for the raw colour view.
                        CameraCheck.Frames(Time.unscaledTime);
                        // Upload only the newest frame each Unity update; don't replay old preview frames.
                        newestPreview = message;
                        break;
                    case "ready":
                        StatusText = "Wellness camera warming up…";
                        break;
                    case "recovering":
                        HasSample = false;
                        CameraCheck.Metrics(false, false, Time.unscaledTime);
                        CameraCheck.Validation(-1, Time.unscaledTime);
                        State = PresageBiometricState.NeedsAttention;
                        ValidationHint = StatusText = Safe(message.message);
                        UnityEngine.Debug.Log("[MindSpace Presage] Recovering frame timing (code " + message.errorCode + "); camera remains connected.", this);
                        break;
                    case "recovered":
                        StatusText = "Camera timing restored. Waiting for fresh position and measurements…";
                        break;
                    case "status":
                        ApplyStatus(message.statusCode);
                        break;
                    case "validation":
                        CameraCheck.Validation(message.validationCode, Time.unscaledTime);
                        ValidationHint = Safe(message.hint);
                        if (message.validationCode == 0)
                        {
                            if (State == PresageBiometricState.NeedsAttention) State = PresageBiometricState.Measuring;
                            StatusText = HasSample ? "Wellness measurements active" : "Building a stable baseline…";
                        }
                        else
                        {
                            HasSample = false;
                            State = PresageBiometricState.NeedsAttention;
                            StatusText = string.IsNullOrEmpty(ValidationHint) ? "Adjust camera position or lighting" : ValidationHint;
                        }
                        break;
                    case "metrics":
                        if (State == PresageBiometricState.NeedsAttention) break;
                        LatestSample = new PresageBiometricSample(message.timestampUs / 1_000_000d,
                            message.hasPulse, message.pulseBpm, message.pulseConfidence, message.pulseStable,
                            message.hasBreathing, message.breathingRate, message.breathingConfidence,
                            message.breathingStable, message.hasHrv, message.hrvRmssd, message.hrvSdnn,
                            message.hrvConfidence);
                        HasSample = true;
                        CameraCheck.Metrics(LatestSample.PulseIsUsable(60), LatestSample.BreathingIsUsable(60), Time.unscaledTime);
                        lastSampleAt = Time.unscaledTime;
                        if (State != PresageBiometricState.NeedsAttention) State = PresageBiometricState.Measuring;
                        StatusText = "Wellness measurements active";
                        SampleUpdated?.Invoke(LatestSample);
                        break;
                    case "error":
                        newestPreview = null;
                        Fail(Safe(message.message));
                        StopSidecar(PresageBiometricState.Error, StatusText);
                        break;
                    case "exited":
                        newestPreview = null;
                        colourCamera?.Dispose();colourCamera=null;
                        CameraCheck.Reset(); ClearPreview();
                        Process exited = sidecar;
                        sidecar = null;
                        stopping = false;
                        exited?.Dispose();
                        if (resumeAfterStop && HasConsent)
                        {
                            resumeAfterStop = false;
                            StartMeasurement();
                        }
                        else if (State == PresageBiometricState.Starting || IsMeasuring)
                        {
                            Fail("Wellness camera stopped unexpectedly. Resume it from voice controls.");
                        }
                        break;
                }
            }
            if(colourCamera!=null&&HasConsent&&!stopping)
            {
                try
                {
                    colourCamera.Tick(previewEnabled);
                    if(colourCamera.Error!=null)
                    {
                        Fail(colourCamera.Error);
                        StopSidecar(PresageBiometricState.Error,StatusText);
                    }
                    else if(colourCamera.PreviewChanged)
                        UploadPreview(colourCamera.PreviewRgb,colourCamera.PreviewWidth,colourCamera.PreviewHeight);
                }
                catch(Exception)
                {
                    Fail("Camera capture interrupted. Check camera access and retry.");
                    StopSidecar(PresageBiometricState.Error,StatusText);
                }
            }
            if (newestPreview != null && !stopping && HasConsent &&
                State != PresageBiometricState.Error && State != PresageBiometricState.Paused)
                UpdatePreview(newestPreview);
            CameraCheck.Tick(Time.unscaledTime);
        }

        private void UpdatePreview(WireMessage message)
        {
            if (!previewEnabled || message.width < 1 || message.width > 320 || message.height < 1 || message.height > 640 || string.IsNullOrEmpty(message.rgb)) return;
            try
            {
                byte[] pixels = Convert.FromBase64String(message.rgb);
                if (pixels.Length != message.width * message.height * 3) return;
                UploadPreview(pixels,message.width,message.height);
            }
            catch (FormatException) { }
        }

        private void UploadPreview(byte[] pixels,int width,int height)
        {
            if(CameraPreview==null||CameraPreview.width!=width||CameraPreview.height!=height)
            {
                ClearPreview();
                CameraPreview=new Texture2D(width,height,TextureFormat.RGB24,false)
                {hideFlags=HideFlags.HideAndDontSave,filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};
            }
            CameraPreview.LoadRawTextureData(pixels);CameraPreview.Apply(false);previewAt=Time.unscaledTime;
        }

        private void ApplyStatus(int code)
        {
            CameraCheck.SetRunning(code == 3);
            if (code == 2) { State = PresageBiometricState.Starting; StatusText = "Starting wellness camera…"; }
            else if (code == 3) { State = PresageBiometricState.Measuring; StatusText = HasSample ? "Wellness measurements active" : "Building a stable baseline…"; }
            else if (code == 5) Fail("Presage measurement entered an error state.");
        }

        private void StopMeasurement(bool clearConsent)
        {
            resumeAfterStop = false;
            StopSidecar(clearConsent ? PresageBiometricState.Off : PresageBiometricState.Paused,
                clearConsent ? "Wellness camera off" : "Wellness camera paused");
            if (clearConsent)
            {
                HasConsent = false;
                ProactiveGuidance = false;
            }
        }

        private void StopSidecar(PresageBiometricState finalState, string finalStatus)
        {
            colourCamera?.Dispose();colourCamera=null;
            Process process = sidecar;
            State = finalState;
            StatusText = finalStatus;
            ValidationHint = string.Empty;
            HasSample = false;
            CameraCheck.Reset(); ClearPreview();
            if (process == null || HasExited(process)) return;
            if (stopping) return;
            stopping = true;
            try
            {
                process.StandardInput.WriteLine("stop");
                process.StandardInput.Flush();
                process.StandardInput.Close();
            }
            catch { }
            if (isActiveAndEnabled && gameObject.activeInHierarchy) StartCoroutine(EnsureStopped(process, generation));
            else { try { process.Kill(); } catch { } }
        }

        private IEnumerator EnsureStopped(Process process, int epoch)
        {
            float deadline = Time.realtimeSinceStartup + 2;
            while (process != null && !HasExited(process) && Time.realtimeSinceStartup < deadline) yield return null;
            if (process != null && !HasExited(process))
            {
                try { process.Kill(); } catch { }
            }
        }

        private void Fail(string message)
        {
            colourCamera?.Dispose();colourCamera=null;
            CameraCheck.Reset(); ClearPreview();
            State = PresageBiometricState.Error;
            StatusText = string.IsNullOrWhiteSpace(message) ? "Wellness camera unavailable" : Safe(message);
            UnityEngine.Debug.LogWarning("[MindSpace Presage] " + StatusText, this);
        }

        private static bool HasExited(Process process)
        {
            try { return process == null || process.HasExited; }
            catch { return true; }
        }

        private static string ResolveNodeExecutable()
        {
            string configured = ReadSetting("TLW_NODE_PATH");
            return string.IsNullOrWhiteSpace(configured) ? "node" : configured;
        }

        // Prefer saved Windows settings so rotation works without restarting every parent process.
        public static string ReadSetting(string name)
        {
            if (Application.platform == RuntimePlatform.WindowsEditor || Application.platform == RuntimePlatform.WindowsPlayer)
            {
                foreach (EnvironmentVariableTarget target in new[] { EnvironmentVariableTarget.User, EnvironmentVariableTarget.Machine })
                {
                    try
                    {
                        string saved = System.Environment.GetEnvironmentVariable(name, target);
                        if (!string.IsNullOrWhiteSpace(saved)) return saved.Trim();
                    }
                    catch (PlatformNotSupportedException) { }
                    catch (System.Security.SecurityException) { }
                }
            }
            string inherited = System.Environment.GetEnvironmentVariable(name);
            return string.IsNullOrWhiteSpace(inherited) ? null : inherited.Trim();
        }

        private static string ResolveBridgeScript()
        {
            string configured = ReadSetting("TLW_PRESAGE_BRIDGE_PATH");
            if (!string.IsNullOrWhiteSpace(configured)) return configured;
            string streamed = Path.Combine(Application.streamingAssetsPath, "TherapyGame", "PresageBridge", "bridge.mjs");
            if (File.Exists(streamed)) return streamed;
            return Path.Combine(Application.dataPath, "TherapyGame", "Integrations", "PresageBridge", "bridge.mjs");
        }

        private static string Quote(string value) => "\"" + value.Replace("\"", "\\\"") + "\"";

        private string Safe(string value)
        {
            string result = string.IsNullOrWhiteSpace(value) ? "Wellness camera unavailable" : value.Replace('\r', ' ').Replace('\n', ' ');
            if (!string.IsNullOrEmpty(activeKey)) result = result.Replace(activeKey, "[redacted]");
            foreach (string name in new[] { "TLW_PRESAGE_API_KEY", "SMARTSPECTRA_API_KEY" })
            {
                string secret = ReadSetting(name);
                if (!string.IsNullOrEmpty(secret)) result = result.Replace(secret, "[redacted]");
            }
            return result.Length > 240 ? result.Substring(0, 240) : result;
        }

        private void OnApplicationFocus(bool focused)
        {
            if (!focused && HasConsent) PauseMeasurement();
        }

        private void OnDisable()
        {
            StopMeasurement(true);
        }
    }
}
