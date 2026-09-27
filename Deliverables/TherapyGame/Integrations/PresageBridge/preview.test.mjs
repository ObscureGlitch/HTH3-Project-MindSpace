import { test } from 'node:test';
import assert from 'node:assert/strict';
import { previewFrame } from './preview.mjs';

test('RGB preview is mirrored and vertically flipped for Unity', () => {
  const data = Buffer.from([255,0,0, 0,255,0, 0,0,255, 255,255,255]);
  const frame = previewFrame(data,2,2,6,0);
  assert.deepEqual([...Buffer.from(frame.rgb,'base64')],[255,255,255,0,0,255,0,255,0,255,0,0]);
});
test('BGRA discards alpha and respects row stride', () => {
  const frame = previewFrame(Buffer.from([1,2,3,255,99,99,99,99]),1,1,8,3);
  assert.deepEqual([...Buffer.from(frame.rgb,'base64')],[3,2,1]);
});
test('invalid buffer and unsupported formats do not produce misleading preview', () => {
  assert.equal(previewFrame(Buffer.alloc(2),1,1,3,0),null);
  assert.equal(previewFrame(Buffer.alloc(8),2,2,2,4),null);
});
