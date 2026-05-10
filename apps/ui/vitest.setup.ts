import '@testing-library/jest-dom/vitest';
import { cleanup } from '@testing-library/react';
import { vi, afterEach } from 'vitest';

afterEach(cleanup);

// Shim jest globals for tests written with Jest API
globalThis.jest = { fn: vi.fn, spyOn: vi.spyOn, clearAllMocks: vi.clearAllMocks, advanceTimersByTime: vi.advanceTimersByTime.bind(vi) } as any;
