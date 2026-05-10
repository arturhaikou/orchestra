import { describe, it, expect } from 'vitest';
import {
  validateNameLocally,
  validateUrl,
  resolveApiKeyOk,
  resolveApiKeyRequiredError,
} from '../useHttpFieldsValidation';
import { McpServerHttpFields } from '../../types';

// ─── Test data factory helpers ────────────────────────────────────────────────

const httpFields = (overrides: Partial<McpServerHttpFields & { serverName: string }> = {}) =>
  ({
    serverName: 'My Server',
    url: 'https://example.com/mcp',
    authType: 'none',
    apiKey: '',
    ...overrides,
  } as McpServerHttpFields & { serverName: string });

// ─── validateNameLocally ──────────────────────────────────────────────────────

describe('validateNameLocally', () => {
  it('ValidateName_WhenEmpty_ReturnsRequired', () => {
    expect(validateNameLocally('')).toBe('Server name is required.');
  });

  it('ValidateName_WhenWhitespaceOnly_ReturnsRequired', () => {
    expect(validateNameLocally('   ')).toBe('Server name is required.');
  });

  it('ValidateName_WhenSingleChar_ReturnsTooShort', () => {
    expect(validateNameLocally('a')).toBe('Server name must be at least 2 characters.');
  });

  it('ValidateName_WhenMinLength_ReturnsUndefined', () => {
    expect(validateNameLocally('ab')).toBeUndefined();
  });

  it('ValidateName_WhenValidName_ReturnsUndefined', () => {
    expect(validateNameLocally('My Server')).toBeUndefined();
  });
});

// ─── validateUrl ─────────────────────────────────────────────────────────────

describe('validateUrl', () => {
  it('ValidateUrl_WhenEmpty_ReturnsRequired', () => {
    expect(validateUrl('')).toBeTruthy();
  });

  it('ValidateUrl_WhenNonHttps_ReturnsHttpsRequired', () => {
    expect(validateUrl('http://example.com/mcp')).toBe('URL must use HTTPS.');
  });

  it('ValidateUrl_WhenMissingScheme_ReturnsInvalidUrl', () => {
    expect(validateUrl('example.com/mcp')).toBe('Please enter a valid URL.');
  });

  it('ValidateUrl_WhenValidHttps_ReturnsUndefined', () => {
    expect(validateUrl('https://example.com/mcp')).toBeUndefined();
  });

  it('ValidateUrl_WhenHttpsWithPath_ReturnsUndefined', () => {
    expect(validateUrl('https://api.company.com/v1/mcp?workspace=abc')).toBeUndefined();
  });
});

// ─── resolveApiKeyRequiredError ───────────────────────────────────────────────

describe('resolveApiKeyRequiredError', () => {
  it('ApiKeyError_WhenAuthNone_ReturnsUndefined', () => {
    const fields = httpFields({ authType: 'none', apiKey: '' });
    expect(resolveApiKeyRequiredError(fields, false, 'touched')).toBeUndefined();
  });

  it('ApiKeyError_WhenApiKeyAuthAndEmptyKey_ReturnsError', () => {
    const fields = httpFields({ authType: 'api_key', apiKey: '' });
    expect(resolveApiKeyRequiredError(fields, false, 'touched')).toBe(
      'API Key is required when using API Key authentication.'
    );
  });

  it('ApiKeyError_WhenApiKeyAuthAndPopulatedKey_ReturnsUndefined', () => {
    const fields = httpFields({ authType: 'api_key', apiKey: 'secret-key-123' });
    expect(resolveApiKeyRequiredError(fields, false, 'touched')).toBeUndefined();
  });

  it('ApiKeyError_WhenEditModeAndMasked_ReturnsUndefined', () => {
    const fields = httpFields({ authType: 'api_key', apiKey: '' });
    expect(resolveApiKeyRequiredError(fields, true, 'masked')).toBeUndefined();
  });
});

// ─── resolveApiKeyOk ─────────────────────────────────────────────────────────

describe('resolveApiKeyOk', () => {
  it('ApiKeyOk_WhenAuthNoneAndNoErrors_ReturnsTrue', () => {
    const fields = httpFields({ authType: 'none', apiKey: '' });
    expect(resolveApiKeyOk(fields, false, 'touched', undefined)).toBe(true);
  });

  it('ApiKeyOk_WhenApiKeyAuthWithValueAndNoErrors_ReturnsTrue', () => {
    const fields = httpFields({ authType: 'api_key', apiKey: 'secret' });
    expect(resolveApiKeyOk(fields, false, 'touched', undefined)).toBe(true);
  });

  it('ApiKeyOk_WhenApiKeyAuthEmptyAndHasError_ReturnsFalse', () => {
    const fields = httpFields({ authType: 'api_key', apiKey: '' });
    const error = 'API Key is required when using API Key authentication.';
    expect(resolveApiKeyOk(fields, false, 'touched', error)).toBe(false);
  });
});
