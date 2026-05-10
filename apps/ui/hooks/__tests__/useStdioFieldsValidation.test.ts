import { describe, it, expect, vi, beforeEach } from 'vitest';
import {
  validateCommand,
  validateArgument,
  validateEnvKey,
  calculateEnvTotalSize,
  isStdioFormValid,
} from '../useStdioFieldsValidation';
import { McpServerStdioFields, StdioFieldErrors, EnvVar } from '../../types';

vi.mock('../../services/mcpServerService', () => ({
  checkMcpServerNameUnique: vi.fn().mockResolvedValue({ isUnique: true }),
}));

beforeEach(() => vi.clearAllMocks());

// ─── Test data helpers ────────────────────────────────────────────────────────

const stdioFields = (
  overrides: Partial<McpServerStdioFields & { serverName: string }> = {}
): McpServerStdioFields & { serverName: string } => ({
  serverName: 'My Server',
  command: 'npx',
  args: [],
  envVars: [],
  ...overrides,
});

const envVar = (key: string, value: string): EnvVar => ({ key, value });

// ─── validateCommand ─────────────────────────────────────────────────────────

describe('validateCommand', () => {
  it('ValidateCommand_WhenEmpty_ReturnsRequired', () => {
    expect(validateCommand('')).toBe('Command is required.');
  });

  it('ValidateCommand_WhenWhitespaceOnly_ReturnsRequired', () => {
    expect(validateCommand('   ')).toBe('Command is required.');
  });

  it('ValidateCommand_WhenExceeds500Chars_ReturnsTooLong', () => {
    expect(validateCommand('a'.repeat(501))).toBe(
      'Command must not exceed 500 characters.'
    );
  });

  it('ValidateCommand_WhenExactly500Chars_ReturnsUndefined', () => {
    expect(validateCommand('a'.repeat(500))).toBeUndefined();
  });

  it('ValidateCommand_WhenContainsAmpersand_ReturnsShellError', () => {
    expect(validateCommand('npx && rm -rf /')).toBe(
      'Shell operators are not allowed. Enter a single executable name.'
    );
  });

  it('ValidateCommand_WhenContainsPipe_ReturnsShellError', () => {
    expect(validateCommand('npx | cat')).toBe(
      'Shell operators are not allowed. Enter a single executable name.'
    );
  });

  it('ValidateCommand_WhenContainsRedirect_ReturnsShellError', () => {
    expect(validateCommand('echo foo > file')).toBe(
      'Shell operators are not allowed. Enter a single executable name.'
    );
  });

  it('ValidateCommand_WhenContainsSemicolon_ReturnsShellError', () => {
    expect(validateCommand('npx; ls')).toBe(
      'Shell operators are not allowed. Enter a single executable name.'
    );
  });

  it('ValidateCommand_WhenContainsBacktick_ReturnsShellError', () => {
    expect(validateCommand('`whoami`')).toBe(
      'Shell operators are not allowed. Enter a single executable name.'
    );
  });

  it('ValidateCommand_WhenContainsSubshell_ReturnsShellError', () => {
    expect(validateCommand('$(rm -rf /)')).toBe(
      'Shell operators are not allowed. Enter a single executable name.'
    );
  });

  it('ValidateCommand_WhenValidSingleExecutable_ReturnsUndefined', () => {
    expect(validateCommand('npx')).toBeUndefined();
  });

  it('ValidateCommand_WhenValidPathExecutable_ReturnsUndefined', () => {
    expect(validateCommand('/usr/local/bin/python3')).toBeUndefined();
  });

  it('ValidateCommand_WhenValidDockerCommand_ReturnsUndefined', () => {
    expect(validateCommand('docker')).toBeUndefined();
  });

  it('ValidateCommand_EmptyValidatedBeforeLength_PrioritisesRequired', () => {
    expect(validateCommand('')).toBe('Command is required.');
  });
});

// ─── validateArgument ────────────────────────────────────────────────────────

describe('validateArgument', () => {
  it('ValidateArgument_WhenExceeds1000Chars_ReturnsTooLong', () => {
    expect(validateArgument('a'.repeat(1001))).toBe(
      'Argument exceeds the maximum length of 1,000 characters.'
    );
  });

  it('ValidateArgument_WhenExactly1000Chars_ReturnsUndefined', () => {
    expect(validateArgument('a'.repeat(1000))).toBeUndefined();
  });

  it('ValidateArgument_WhenEmpty_ReturnsUndefined', () => {
    expect(validateArgument('')).toBeUndefined();
  });

  it('ValidateArgument_WhenValidShortArg_ReturnsUndefined', () => {
    expect(validateArgument('-y')).toBeUndefined();
  });
});

// ─── validateEnvKey ──────────────────────────────────────────────────────────

describe('validateEnvKey', () => {
  it('ValidateEnvKey_WhenEmpty_ReturnsRequired', () => {
    expect(validateEnvKey('', [], 0)).toBe('Key is required.');
  });

  it('ValidateEnvKey_WhenContainsSpace_ReturnsInvalidChars', () => {
    expect(validateEnvKey('MY VAR', [], 0)).toBe(
      'Key must contain only letters, numbers, and underscores.'
    );
  });

  it('ValidateEnvKey_WhenContainsDash_ReturnsInvalidChars', () => {
    expect(validateEnvKey('MY-VAR', [], 0)).toBe(
      'Key must contain only letters, numbers, and underscores.'
    );
  });

  it('ValidateEnvKey_WhenContainsDot_ReturnsInvalidChars', () => {
    expect(validateEnvKey('MY.VAR', [], 0)).toBe(
      'Key must contain only letters, numbers, and underscores.'
    );
  });

  it('ValidateEnvKey_WhenDuplicate_ReturnsDuplicateError', () => {
    const allKeys = ['GITHUB_TOKEN', 'NODE_ENV', 'GITHUB_TOKEN'];
    expect(validateEnvKey('GITHUB_TOKEN', allKeys, 2)).toBe(
      'Key must be unique within this server\'s environment variables.'
    );
  });

  it('ValidateEnvKey_WhenUniqueInSet_ReturnsUndefined', () => {
    const allKeys = ['GITHUB_TOKEN', 'NODE_ENV'];
    expect(validateEnvKey('GITHUB_TOKEN', allKeys, 0)).toBeUndefined();
  });

  it('ValidateEnvKey_WhenValidAlphanumericUnderscore_ReturnsUndefined', () => {
    expect(validateEnvKey('MY_VAR_123', [], 0)).toBeUndefined();
  });

  it('ValidateEnvKey_WhenAllUppercase_ReturnsUndefined', () => {
    expect(validateEnvKey('GITHUB_TOKEN', [], 0)).toBeUndefined();
  });
});

// ─── calculateEnvTotalSize ───────────────────────────────────────────────────

describe('calculateEnvTotalSize', () => {
  it('CalculateEnvTotalSize_WhenEmpty_ReturnsZero', () => {
    expect(calculateEnvTotalSize([])).toBe(0);
  });

  it('CalculateEnvTotalSize_WhenSingleRow_ReturnsSumOfKeyAndValue', () => {
    expect(calculateEnvTotalSize([envVar('KEY', 'value')])).toBe(8);
  });

  it('CalculateEnvTotalSize_WhenMultipleRows_ReturnsCombinedSum', () => {
    const vars = [envVar('A', 'abc'), envVar('BB', 'de')];
    expect(calculateEnvTotalSize(vars)).toBe(1 + 3 + 2 + 2);
  });

  it('CalculateEnvTotalSize_WhenAt4096_ReturnsExactLimit', () => {
    const key = 'K'.repeat(100);
    const value = 'v'.repeat(3996);
    expect(calculateEnvTotalSize([envVar(key, value)])).toBe(4096);
  });

  it('CalculateEnvTotalSize_WhenOver4096_ReturnsValueAboveLimit', () => {
    const key = 'K'.repeat(100);
    const value = 'v'.repeat(4000);
    expect(calculateEnvTotalSize([envVar(key, value)])).toBeGreaterThan(4096);
  });
});

// ─── isStdioFormValid ────────────────────────────────────────────────────────

describe('isStdioFormValid', () => {
  it('IsStdioFormValid_WhenAllFieldsValidAndNoErrors_ReturnsTrue', () => {
    const fields = stdioFields({ serverName: 'My Server', command: 'npx' });
    const errors: StdioFieldErrors = {};
    expect(isStdioFormValid(fields, errors, false)).toBe(true);
  });

  it('IsStdioFormValid_WhenCommandEmpty_ReturnsFalse', () => {
    const fields = stdioFields({ command: '' });
    const errors: StdioFieldErrors = { command: 'Command is required.' };
    expect(isStdioFormValid(fields, errors, false)).toBe(false);
  });

  it('IsStdioFormValid_WhenServerNameError_ReturnsFalse', () => {
    const fields = stdioFields({ serverName: 'a' });
    const errors: StdioFieldErrors = {
      serverName: 'Server name must be at least 2 characters.',
    };
    expect(isStdioFormValid(fields, errors, false)).toBe(false);
  });

  it('IsStdioFormValid_WhenCheckingName_ReturnsFalse', () => {
    const fields = stdioFields({ serverName: 'My Server', command: 'npx' });
    const errors: StdioFieldErrors = {};
    expect(isStdioFormValid(fields, errors, true)).toBe(false);
  });

  it('IsStdioFormValid_WhenArgError_ReturnsFalse', () => {
    const fields = stdioFields({ command: 'npx', args: ['a'.repeat(1001)] });
    const errors: StdioFieldErrors = {
      argErrors: { 0: 'Argument exceeds the maximum length of 1,000 characters.' },
    };
    expect(isStdioFormValid(fields, errors, false)).toBe(false);
  });

  it('IsStdioFormValid_WhenEnvKeyError_ReturnsFalse', () => {
    const fields = stdioFields({
      command: 'npx',
      envVars: [envVar('BAD KEY', 'val')],
    });
    const errors: StdioFieldErrors = {
      envKeyErrors: {
        0: 'Key must contain only letters, numbers, and underscores.',
      },
    };
    expect(isStdioFormValid(fields, errors, false)).toBe(false);
  });

  it('IsStdioFormValid_WhenEnvTotalSizeError_ReturnsFalse', () => {
    const fields = stdioFields({ command: 'npx' });
    const errors: StdioFieldErrors = {
      envTotalSize: 'Environment variables exceed the maximum allowed size.',
    };
    expect(isStdioFormValid(fields, errors, false)).toBe(false);
  });

  it('IsStdioFormValid_WhenServerNameWhitespaceOnly_ReturnsFalse', () => {
    const fields = stdioFields({ serverName: '   ', command: 'npx' });
    const errors: StdioFieldErrors = {};
    expect(isStdioFormValid(fields, errors, false)).toBe(false);
  });

  it('IsStdioFormValid_WhenArgCountExceeds50_ReturnsFalse', () => {
    const fields = stdioFields({ command: 'npx', args: Array(51).fill('arg') });
    const errors: StdioFieldErrors = {
      argErrors: { 50: 'Maximum of 50 arguments allowed.' },
    };
    expect(isStdioFormValid(fields, errors, false)).toBe(false);
  });

  it('IsStdioFormValid_WhenArgCountExactly50_ReturnsTrue', () => {
    const fields = stdioFields({ command: 'npx', args: Array(50).fill('arg') });
    const errors: StdioFieldErrors = {};
    expect(isStdioFormValid(fields, errors, false)).toBe(true);
  });
});

// ─── useStdioFieldsValidation hook — handleBlur('serverName') async check ────

describe('useStdioFieldsValidation hook — handleBlur serverName async uniqueness', () => {
  it('HandleBlur_WhenServerNameBlurred_AndNameNotUnique_SetsServerNameError', async () => {
    const { checkMcpServerNameUnique } = await import('../../services/mcpServerService');
    vi.mocked(checkMcpServerNameUnique).mockResolvedValueOnce({ isUnique: false });

    const { renderHook, act } = await import('@testing-library/react');
    const { useStdioFieldsValidation } = await import('../useStdioFieldsValidation');

    const fields = {
      serverName: 'My Server',
      command: 'npx',
      args: [] as string[],
      envVars: [] as EnvVar[],
    };

    const { result } = renderHook(() =>
      useStdioFieldsValidation(fields, { workspaceId: 'ws-1' })
    );

    await act(async () => {
      await result.current.handleBlur('serverName');
    });

    expect(result.current.errors.serverName).toBe(
      'A server with this name already exists.'
    );
  });

  it('HandleBlur_WhenServerNameBlurred_AndNameIsUnique_ClearsServerNameError', async () => {
    const { checkMcpServerNameUnique } = await import('../../services/mcpServerService');
    vi.mocked(checkMcpServerNameUnique).mockResolvedValueOnce({ isUnique: true });

    const { renderHook, act } = await import('@testing-library/react');
    const { useStdioFieldsValidation } = await import('../useStdioFieldsValidation');

    const fields = {
      serverName: 'Unique Name',
      command: 'npx',
      args: [] as string[],
      envVars: [] as EnvVar[],
    };

    const { result } = renderHook(() =>
      useStdioFieldsValidation(fields, { workspaceId: 'ws-1' })
    );

    await act(async () => {
      await result.current.handleBlur('serverName');
    });

    expect(result.current.errors.serverName).toBeUndefined();
  });
});

// ─── useStdioFieldsValidation hook — handleBlur('envKey') total size check ───

describe('useStdioFieldsValidation hook — handleBlur envKey total size', () => {
  it('HandleBlur_WhenEnvKeyBlurred_AndTotalSizeExceeds4096_SetsEnvTotalSizeError', async () => {
    const { renderHook, act } = await import('@testing-library/react');
    const { useStdioFieldsValidation } = await import('../useStdioFieldsValidation');

    const bigValue = 'v'.repeat(4090);
    const fields = {
      serverName: 'My Server',
      command: 'npx',
      args: [] as string[],
      envVars: [
        { key: 'K1', value: bigValue },
        { key: 'K2', value: 'extra' },
      ],
    };

    const { result } = renderHook(() =>
      useStdioFieldsValidation(fields, { workspaceId: 'ws-1' })
    );

    await act(async () => {
      await result.current.handleBlur('envKey', 1);
    });

    expect(result.current.errors.envTotalSize).toBe(
      'Environment variables exceed the maximum allowed size.'
    );
  });

  it('HandleBlur_WhenEnvKeyBlurred_AndTotalSizeExactly4096_LeavesEnvTotalSizeUndefined', async () => {
    const { renderHook, act } = await import('@testing-library/react');
    const { useStdioFieldsValidation } = await import('../useStdioFieldsValidation');

    const key = 'K'.repeat(100);
    const value = 'v'.repeat(3996);
    const fields = {
      serverName: 'My Server',
      command: 'npx',
      args: [] as string[],
      envVars: [{ key, value }],
    };

    const { result } = renderHook(() =>
      useStdioFieldsValidation(fields, { workspaceId: 'ws-1' })
    );

    await act(async () => {
      await result.current.handleBlur('envKey', 0);
    });

    expect(result.current.errors.envTotalSize).toBeUndefined();
  });
});
