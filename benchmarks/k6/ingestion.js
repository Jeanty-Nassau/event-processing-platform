import http from 'k6/http';
import crypto from 'k6/crypto';
import { check } from 'k6';

const baseUrl = __ENV.BASE_URL || 'http://localhost:8080';
const secret = __ENV.EVENT_SIGNING_SECRET || 'local-development-secret';
const subjectCount = Number(__ENV.SUBJECT_COUNT || 1000);

export const options = {
  vus: Number(__ENV.VUS || 25),
  duration: __ENV.DURATION || '30s',
};

function randomUuid() {
  const s = 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx';
  return s.replace(/[xy]/g, (c) => {
    const r = Math.floor(Math.random() * 16);
    const v = c === 'x' ? r : (r & 0x3) | 0x8;
    return v.toString(16);
  });
}

export default function () {
  const event = {
    eventId: randomUuid(),
    eventType: 'demo.work.requested',
    source: 'k6',
    subjectId: `subject-${Math.floor(Math.random() * subjectCount)}`,
    schemaVersion: 1,
    occurredAt: new Date().toISOString(),
    correlationId: randomUuid(),
    payload: { operation: 'transform', value: 42 },
  };

  const body = JSON.stringify(event);
  const digest = crypto.hmac('sha256', secret, body, 'hex');

  const response = http.post(`${baseUrl}/api/v1/events`, body, {
    headers: {
      'Content-Type': 'application/json',
      'X-Signature': `sha256=${digest}`,
    },
  });

  check(response, {
    'accepted': (r) => r.status === 202,
  });
}
