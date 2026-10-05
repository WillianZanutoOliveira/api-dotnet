import http from 'k6/http';
import { check, sleep } from 'k6';

const baseUrl = __ENV.ORDERS_API_BASE || 'http://localhost:8081';
const token = __ENV.K6_ACCESS_TOKEN;

if (!token) {
  throw new Error('K6_ACCESS_TOKEN is required.');
}

export const options = {
  stages: [
    { duration: '10s', target: 2 },
    { duration: '20s', target: 5 },
    { duration: '20s', target: 5 },
    { duration: '10s', target: 0 },
  ],
  thresholds: {
    http_req_failed: ['rate<0.01'],
    http_req_duration: ['p(95)<1000'],
    checks: ['rate>0.99'],
  },
};

export default function () {
  const payload = JSON.stringify({
    items: [
      {
        sku: `K6-${__VU}-${__ITER}`,
        quantity: 1,
        unitPrice: 99.9,
      },
    ],
  });

  const response = http.post(`${baseUrl}/orders`, payload, {
    headers: {
      Authorization: `Bearer ${token}`,
      'Content-Type': 'application/json',
    },
    tags: {
      operation: 'create-order',
    },
  });

  check(response, {
    'order created': (result) => result.status === 201,
  });

  sleep(1);
}
