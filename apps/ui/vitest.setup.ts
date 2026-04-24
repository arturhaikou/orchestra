import '@testing-library/jest-dom/vitest';
import { vi } from 'vitest';

// Shim jest globals for tests written with Jest API
globalThis.jest = { fn: vi.fn, spyOn: vi.spyOn, clearAllMocks: vi.clearAllMocks } as any;
