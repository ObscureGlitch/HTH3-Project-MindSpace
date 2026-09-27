using System;
using System.Collections.Generic;

namespace TheLastWatch.Integrations
{
    // Main-thread, memory-only text. No audio samples or additional microphone reader.
    public sealed class WellnessCaptions
    {
        public enum Speaker { Waiting, Player, Companion, Listening }
        public Speaker Who { get; private set; } = Speaker.Waiting;
        public string Message { get; private set; } = "...";
        public int EventId { get; private set; }
        public string DisplayText { get; private set; } = "...";
        // Compatibility for older editor checks; the HUD no longer pages captions.
        public string[] Pages => new[] { DisplayText };
        public int Page => 0;
        public float RevealAge(float now) => Math.Max(0, now - revealedAt);
        private const int MaxText = 4000;
        private struct Cue { public float at; public int end; }
        private readonly List<Cue> cues = new List<Cue>();
        private string streamed = "", aligned = "";
        private string renderedMessage;
        private int renderedLength=-1;
        private int cueIndex, visible, newestAgent = -1, interruptedThrough = -1;
        private float changedAt, revealedAt, alignmentEnd, fallbackAt;
        private bool wasSpeaking, hasAlignment, finalReceived;

        public void Reset()
        {
            newestAgent = interruptedThrough = -1; wasSpeaking = false;
            Set(Speaker.Waiting, "...", 0, 0);
        }
        private void Set(Speaker who, string text, int id, float now)
        {
            Who = who; Message = Bounded(text); EventId = id; changedAt = now;
            streamed = aligned = ""; cues.Clear(); cueIndex = visible = 0;
            renderedMessage=null;renderedLength=-1;
            hasAlignment = finalReceived = false; alignmentEnd = now; fallbackAt = float.MaxValue;
            Show(who == Speaker.Companion ? "..." : Message, now);
        }
        private void Show(string text, float now)
        {
            if (text == DisplayText) return;
            DisplayText = text; revealedAt = now;
        }
        private bool BeginAgent(int id, float now)
        {
            if (id <= interruptedThrough || id < newestAgent) return false;
            if (Who != Speaker.Companion || EventId != id)
            {
                // Do not resurrect a completed turn from a delayed network chunk.
                if (id == newestAgent) return false;
                newestAgent = id; Set(Speaker.Companion, "", id, now);
            }
            return true;
        }
        public void User(string text, int id, float now)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            // Provider sends finalized user utterances: display immediately, without fake typing.
            Set(Speaker.Player, text, id, now);
        }
        public void Agent(string text, int id, float now)
        {
            if (string.IsNullOrWhiteSpace(text) || !BeginAgent(id, now)) return;
            Message = Bounded(text); finalReceived = true;
            // Give the first audio-alignment chunk a short chance to arrive before using
            // the completed-message fallback. Do not artificially type out a final transcript.
            if (!hasAlignment) fallbackAt = now + .2f;
        }
        public void Stream(string text, string type, int id, float now)
        {
            if (type != "start" && type != "delta" && type != "stop") return;
            if (!BeginAgent(id, now) || finalReceived) return;
            if (type == "start") streamed = "";
            streamed = Bounded(streamed + (text ?? ""));
            if (streamed.Length == 0) return;
            Message = hasAlignment && aligned.Length >= streamed.Length ? aligned : streamed;
            if (!hasAlignment) Show(Message, now);
        }
        public void Align(string[] chars, int[] startsMs, int[] durationsMs, int id, float now)
        {
            if (chars == null || startsMs == null || durationsMs == null || chars.Length == 0 ||
                chars.Length != startsMs.Length || chars.Length != durationsMs.Length) return;
            int lastStart = -1;
            for (int i = 0; i < chars.Length; i++)
            {
                if (startsMs[i] < lastStart || startsMs[i] < 0 || startsMs[i] > 120000 ||
                    durationsMs[i] < 0 || durationsMs[i] > 10000) return;
                lastStart = startsMs[i];
            }
            if (!BeginAgent(id, now) || aligned.Length >= MaxText) return;
            // SDK exposes chunk alignment, not an exact audible playback cursor. Schedule ordered
            // chunks with a small output-buffer margin. Timing depends on seconds, never frame count.
            float origin = Math.Max(now + .04f, alignmentEnd);
            if (!hasAlignment && DisplayText != "...") visible = DisplayText.Length;
            hasAlignment = true;
            for (int i = 0; i < chars.Length && aligned.Length < MaxText; i++)
            {
                string part = chars[i] ?? ""; if (part.Length == 0) continue;
                aligned = Bounded(aligned + part);
                cues.Add(new Cue { at = origin + startsMs[i] / 1000f, end = aligned.Length });
                alignmentEnd = Math.Max(alignmentEnd, origin + (startsMs[i] + durationsMs[i]) / 1000f);
            }
            if (!finalReceived && aligned.Length >= streamed.Length) Message = aligned;
        }
        public void Correct(string text, int id, float now)
        {
            if (Who != Speaker.Companion || EventId != id || string.IsNullOrWhiteSpace(text)) return;
            Message = Bounded(text); cues.Clear(); hasAlignment = false; finalReceived = true;
            Show(Message, now);
        }
        public void Interrupt(float now)
        {
            interruptedThrough = Math.Max(interruptedThrough, newestAgent);
            wasSpeaking = false; Set(Speaker.Waiting, "...", 0, now);
        }
        public void Tick(bool agentSpeaking, bool userSpeaking, bool muted, float now)
        {
            if (Who == Speaker.Companion && finalReceived && !hasAlignment && now >= fallbackAt) Show(Message, now);
            if (Who == Speaker.Companion && hasAlignment)
            {
                while (cueIndex < cues.Count && cues[cueIndex].at <= now)
                    visible = Math.Max(visible, cues[cueIndex++].end);
                int length = SafeEnd(Message, Math.Min(visible, Message.Length));
                if(length!=renderedLength||!ReferenceEquals(renderedMessage,Message))
                {
                    Show(length > 0 ? Message.Substring(0, length) : "...", now);
                    renderedLength=length;renderedMessage=Message;
                }
            }
            if (agentSpeaking) wasSpeaking = true;
            else if (wasSpeaking)
            {
                wasSpeaking = false;
                if (Who == Speaker.Companion) Set(Speaker.Waiting, "...", 0, now);
            }
            if (!agentSpeaking && !muted && userSpeaking && (Who != Speaker.Player || now - changedAt > 1.5f))
            { if (Who != Speaker.Listening) Set(Speaker.Listening, "Listening…", 0, now); }
            else if (Who == Speaker.Listening && (!userSpeaking || muted)) Set(Speaker.Waiting, "...", 0, now);
            if (!agentSpeaking && ((Who == Speaker.Player && now - changedAt > 8) ||
                (Who == Speaker.Companion && now - changedAt > 30))) Set(Speaker.Waiting, "...", 0, now);
        }
        public static string Bounded(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            return text.Substring(0, SafeEnd(text, Math.Min(text.Length, MaxText)));
        }
        private static int SafeEnd(string text, int end)
        {
            if (end > 0 && end < text.Length && char.IsHighSurrogate(text[end - 1]) && char.IsLowSurrogate(text[end])) end--;
            return end;
        }
        public static string[] Split(string text, int maxChars)
        {
            if (string.IsNullOrEmpty(text)) return new[] { "..." };
            maxChars = Math.Max(32, maxChars); var result = new List<string>(); int start = 0;
            while (start < text.Length)
            {
                int end = SafeEnd(text, Math.Min(start + maxChars, text.Length));
                result.Add(text.Substring(start, end - start)); start = end;
            }
            return result.ToArray();
        }
    }
}
