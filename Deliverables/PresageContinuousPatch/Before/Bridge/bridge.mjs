#!/usr/bin/env node
import { previewFrame } from './preview.mjs';
import { SignalCache, SignalDelivery } from './signals.mjs';
import { FrameDecoder, FrameSubmission, isFrameTimingError } from './frames.mjs';
import { createConnection } from 'node:net';

const args = new Map();
for (let i = 2; i < process.argv.length; i += 1) {
  const key = process.argv[i];
  const value = process.argv[i + 1];
  if (key.startsWith('--')) {
    args.set(key, value && !value.startsWith('--') ? value : 'true');
    if (value && !value.startsWith('--')) i += 1;
  }
}

const emit = (message) => process.stdout.write(`${JSON.stringify(message)}\n`);
const finite = (value) => value != null && Number.isFinite(Number(value)) ? Number(value) : null;
const latest = (values) => Array.isArray(values) && values.length > 0 ? values.at(-1) : null;
const bounded = (value, fallback = '') => String(value ?? fallback).replace(/[\r\n]+/g, ' ').slice(0, 240);
const option = (name, fallback) => {
  const value = Number.parseInt(args.get(name) ?? '', 10);
  return Number.isFinite(value) ? value : fallback;
};

if (args.has('--self-test')) {
  emit({ type: 'status', statusCode: 3 });
  emit({
    type: 'metrics', timestampUs: 1, hasPulse: true, pulseBpm: 72,
    pulseConfidence: 92, pulseStable: true, hasBreathing: true,
    breathingRate: 12, breathingConfidence: 90, breathingStable: true,
    hasHrv: true, hrvRmssd: 42, hrvSdnn: 51, hrvConfidence: 85,
  });
  process.exit(0);
}

const apiKey = (process.env.TLW_PRESAGE_API_KEY || process.env.SMARTSPECTRA_API_KEY || '').trim();
if (!apiKey) {
  emit({
    type: 'error', errorCode: -1, retryable: false,
    message: 'Set TLW_PRESAGE_API_KEY to a rotated Presage key before enabling measurements.',
  });
  process.exit(2);
}

let sdk;
let cameraPipe;
let submission;
let recoveryWatch;
let stopping = false;
let lastValidationCode = null;
let lastValidationMs = 0;
let previewEnabled = args.get('--preview') === 'true';
let lastPreviewMs = 0, lastFrameMs = 0;
let lastDiagnosticMs = 0;
const signals = new SignalCache();
const delivery = new SignalDelivery();

const redact = (value) => bounded(String(value ?? '').replaceAll(apiKey, '[redacted]'));
const stop = async (exitCode = 0) => {
  if (stopping) return;
  stopping = true;
  clearInterval(recoveryWatch);
  cameraPipe?.destroy();
  try {
    if (sdk) await sdk.stopAsync();
  } catch {
    // Teardown is best-effort; never expose native error details or credentials.
  }
  try {
    if (sdk) await sdk.destroy();
  } catch {
    // The OS will reclaim the sidecar after exit.
  }
  process.exit(exitCode);
};

try {
  const {
    SmartSpectraSDK, SmartSpectraLogLevel, breathingMetrics, cardioMetrics, decodeMetrics,
  } = await import('@smartspectra/node-sdk');
  sdk = new SmartSpectraSDK({
    apiKey,
    requestedMetrics: [...breathingMetrics, ...cardioMetrics],
    enableTelemetry: false,
    logLevel: SmartSpectraLogLevel.kError,
  });

  sdk.on('frameSentThrough', (sent) => {
    if (!sent || stopping || submission?.restarting || Date.now() - lastFrameMs < 500) return;
    lastFrameMs = Date.now();
    emit({ type: 'frame' });
  });
  sdk.on('videoOutput', (buffer, width, height, stride, format) => {
    // Smooth local preview (~12 fps), with no frame backlog competing with metric delivery.
    if (stopping || args.has('--frame-pipe') || !previewEnabled || Date.now() - lastPreviewMs < 83 || process.stdout.writableLength > 0) return;
    lastPreviewMs = Date.now();
    const frame = previewFrame(buffer, width, height, stride, format);
    if (frame) emit(frame);
  });

  sdk.on('processingStatus', (status) => {
    if (stopping) return;
    emit({ type: 'status', statusCode: finite(status) ?? -1 });
  });

  sdk.on('validationStatus', (code, timestampUs, hint) => {
    if (stopping || submission?.restarting) return;
    const now = Date.now();
    if (code === lastValidationCode && now - lastValidationMs < 2000) return;
    lastValidationCode = code;
    lastValidationMs = now;
    if (code !== 0) signals.clear();
    emit({
      type: 'validation', validationCode: finite(code) ?? -1,
      timestampUs: finite(timestampUs) ?? 0, hint: redact(hint || 'Adjust camera position or lighting.'),
    });
  });

  sdk.on('metrics', (buffer, timestampUs) => {
    if (stopping || submission?.restarting) return;
    const now = Date.now();
    const metrics = decodeMetrics(buffer);
    if (!metrics || Buffer.isBuffer(metrics)) return;
    if (lastValidationCode === 0) signals.ingest(metrics, now);

    const pulse = latest(metrics.cardio?.pulseRate);
    const breathing = latest(metrics.breathing?.rate);
    const hrv = latest(metrics.cardio?.hrv);
    if (args.has('--diagnostics') && now - lastDiagnosticMs >= 2000) {
      lastDiagnosticMs = now;
      emit({ type: 'diagnostic', pulseCount: metrics.cardio?.pulseRate?.length ?? 0,
        breathingCount: metrics.breathing?.rate?.length ?? 0,
        pulseStable: Boolean(pulse?.stable), pulseConfidence: finite(pulse?.confidence) ?? 0,
        breathingStable: Boolean(breathing?.stable), breathingConfidence: finite(breathing?.confidence) ?? 0,
        cachedPulse: signals.snapshot(now).hasPulse, cachedBreathing: signals.snapshot(now).hasBreathing });
    }
    if (lastValidationCode !== 0) return;
    const snapshot = signals.snapshot(now);
    if (!delivery.shouldEmit(snapshot, now, previewEnabled)) return;
    emit({
      type: 'metrics',
      timestampUs: finite(timestampUs) ?? 0,
      ...snapshot,
    });
  });

  sdk.on('error', (code, message, retryable) => {
    if (stopping) return;
    if (submission && isFrameTimingError(code)) {
      submission.reject({code,message});
      return;
    }
    emit({
      type: 'error', errorCode: finite(code) ?? -1,
      retryable: Boolean(retryable), message: redact(message || 'SmartSpectra measurement failed.'),
    });
  });

  const pipeName=args.get('--frame-pipe');
  if(pipeName){
    if(!/^MindSpacePresage-[a-f0-9]{32}$/.test(pipeName))throw new Error('Invalid local camera pipe.');
    sdk.useCustomInput();
  }else sdk.useCamera({
    deviceIndex: option('--camera-index', 0),
    width: option('--width', 1280),
    height: option('--height', 720),
    fps: option('--fps', 30),
  });
  sdk.start();
  if(pipeName){
    submission=new FrameSubmission((...frame)=>sdk.sendFrame(...frame),(state,error)=>{
      if(stopping)return;
      if(state==='recovering') {
        signals.clear();lastValidationCode=null;lastValidationMs=0;
        emit({type:'recovering',errorCode:error.code,message:'Camera is connected. Smoothing frame timing; keep your position…'});
      } else if(state==='recovered') {
        emit({type:'recovered'});
      } else {
        emit({type:'error',errorCode:finite(error?.code)??-1,retryable:false,
          message:redact('Presage frame processing failed: '+(error?.message||'Unknown frame error.'))});
        void stop(1);
      }
    },()=>performance.now(),async()=>{
      if(stopping)return;
      await sdk.stopAsync();
      if(stopping)return;
      signals.clear();lastValidationCode=null;lastValidationMs=0;
      sdk.start();
    });
    recoveryWatch=setInterval(()=>submission.tick(),250);
    const decoder=new FrameDecoder((...frame)=>{if(!stopping)submission.push(...frame)});
    const failCamera=()=>{
      if(stopping)return;
      emit({type:'error',errorCode:-1,retryable:true,message:'Local colour camera disconnected. Retry the camera test.'});
      void stop(1);
    };
    cameraPipe=createConnection('\\\\.\\pipe\\'+pipeName);
    cameraPipe.on('data',chunk=>{
      try{decoder.push(chunk)}catch{
        if(stopping)return;
        emit({type:'error',errorCode:-1,retryable:false,message:'Local camera frame data was invalid. Retry the camera test.'});
        void stop(1);
      }
    });
    cameraPipe.on('error',failCamera);
    cameraPipe.on('end',failCamera);
  }
  emit({ type: 'ready', sdkVersion: bounded(SmartSpectraSDK.version, 'unknown') });
} catch (error) {
  emit({
    type: 'error', errorCode: finite(error?.code) ?? -1,
    retryable: Boolean(error?.retryable), message: redact(error?.message || 'Unable to start SmartSpectra.'),
  });
  await stop(1);
}

process.stdin.setEncoding('utf8');
let input = '';
process.stdin.on('data', (chunk) => {
  input += chunk;
  const lines = input.split(/\r?\n/); input = lines.pop();
  for (const line of lines) {
    const command = line.trim().toLowerCase();
    if (command === 'stop') void stop(0);
    if (command === 'preview-on') previewEnabled = true;
    if (command === 'preview-off') previewEnabled = false;
  }
});
process.stdin.on('end', () => void stop(0));
process.on('SIGINT', () => void stop(0));
process.on('SIGTERM', () => void stop(0));
