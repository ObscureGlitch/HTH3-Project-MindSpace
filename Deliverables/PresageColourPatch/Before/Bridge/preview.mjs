// Low-resolution local preview. Never persisted or sent to a service.
export function previewFrame(buffer, width, height, stride, format) {
  if (![0, 1, 2, 3].includes(format) || width <= 0 || height <= 0) return null;
  const channels = format < 2 ? 3 : 4;
  if (stride < width * channels || buffer.length < stride * height) return null;
  const w = Math.min(320, width), h = Math.max(1, Math.round(height * w / width));
  const rgb = Buffer.alloc(w * h * 3);
  for (let y = 0; y < h; y++) for (let x = 0; x < w; x++) {
    const source = Math.floor(y * height / h) * stride + Math.floor(x * width / w) * channels;
    // Flip vertical rows for Unity's bottom-up texture storage; mirror for a natural self-view.
    const dest = ((h - y - 1) * w + (w - x - 1)) * 3;
    const bgr = format === 1 || format === 3;
    rgb[dest] = buffer[source + (bgr ? 2 : 0)];
    rgb[dest + 1] = buffer[source + 1];
    rgb[dest + 2] = buffer[source + (bgr ? 0 : 2)];
  }
  return { type: 'preview', width: w, height: h, rgb: rgb.toString('base64') };
}
