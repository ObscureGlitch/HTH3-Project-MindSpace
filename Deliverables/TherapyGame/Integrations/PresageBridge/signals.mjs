const last = values => Array.isArray(values) ? values.at(-1) : null;
const valid = value => typeof value === 'number' && Number.isFinite(value);

// Report readiness changes immediately, including regressions. Rate-limit only unchanged state.
export class SignalDelivery {
  constructor() { this.lastAt = -Infinity; this.lastQuality = ''; }
  shouldEmit(sample, now, previewEnabled) {
    const quality = [sample.hasPulse, sample.pulseStable && sample.pulseConfidence >= 60,
      sample.hasBreathing, sample.breathingStable && sample.breathingConfidence >= 60].join(':');
    if (quality === this.lastQuality && now - this.lastAt < (previewEnabled ? 100 : 500)) return false;
    this.lastQuality = quality; this.lastAt = now;
    return true;
  }
}

// SDK metrics packets are sparse. Merge every packet before throttling the UI snapshot.
// Do not let repeated timestamps or unrelated waveform packets refresh an old rate.
export class SignalCache {
  constructor(ttlMs = 8000) { this.ttlMs = ttlMs; this.clear(); }
  clear() { this.samples = {}; }
  ingest(metrics, now) {
    for (const [name, sample] of Object.entries({
      pulse: last(metrics.cardio?.pulseRate), breathing: last(metrics.breathing?.rate), hrv: last(metrics.cardio?.hrv),
    })) {
      if (!sample) continue;
      const stamp = sample.timestamp == null ? null : String(sample.timestamp);
      const previous = this.samples[name];
      if (stamp && previous?.stamp === stamp) continue;
      this.samples[name] = { sample, stamp, at: now };
    }
  }
  snapshot(now) {
    const get = name => {
      const entry = this.samples[name];
      return entry && now - entry.at <= this.ttlMs ? entry.sample : null;
    };
    const pulse=get('pulse'), breathing=get('breathing'), hrv=get('hrv');
    const number = value => valid(value) ? value : 0;
    return {
      hasPulse: valid(pulse?.value) && pulse.value > 0,
      pulseBpm: number(pulse?.value), pulseConfidence: number(pulse?.confidence), pulseStable: pulse?.stable === true,
      hasBreathing: valid(breathing?.value) && breathing.value > 0,
      breathingRate: number(breathing?.value), breathingConfidence: number(breathing?.confidence), breathingStable: breathing?.stable === true,
      hasHrv: valid(hrv?.rmssd), hrvRmssd: number(hrv?.rmssd), hrvSdnn: number(hrv?.sdnn), hrvConfidence: number(hrv?.confidence),
    };
  }
}
