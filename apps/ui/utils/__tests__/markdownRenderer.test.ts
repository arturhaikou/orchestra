import { renderMarkdown } from '../markdownRenderer';

describe('renderMarkdown', () => {
  describe('Basic markdown rendering', () => {
    it('converts a heading (#) to an <h1> HTML element', () => {
      const result = renderMarkdown('# Hello');
      expect(result).toContain('<h1');
      expect(result).toContain('Hello');
    });

    it('converts bold syntax (**text**) to <strong>', () => {
      const result = renderMarkdown('**bold**');
      expect(result).toContain('<strong>');
    });

    it('converts italic syntax (*text*) to <em>', () => {
      const result = renderMarkdown('*italic*');
      expect(result).toContain('<em>');
    });

    it('converts an unordered list item (- item) to <li>', () => {
      const result = renderMarkdown('- list item');
      expect(result).toContain('<li>');
    });

    it('converts inline code (`code`) to <code>', () => {
      const result = renderMarkdown('`inline code`');
      expect(result).toContain('<code>');
    });

    it('converts a fenced code block to <pre><code>', () => {
      const result = renderMarkdown('```\ncode block\n```');
      expect(result).toContain('<pre>');
      expect(result).toContain('<code>');
    });
  });

  describe('Empty / null-like input', () => {
    it('returns an empty string when input is an empty string', () => {
      const result = renderMarkdown('');
      expect(result.trim()).toBe('');
    });

    it('returns an empty string when input contains only spaces', () => {
      const result = renderMarkdown('   ');
      expect(result.trim()).toBe('');
    });
  });

  describe('HTML injection prevention', () => {
    it('does not pass through raw <script> tags from user input', () => {
      const result = renderMarkdown('<script>alert("xss")</script>');
      expect(result).not.toContain('<script>');
    });

    it('does not pass through raw inline HTML elements from user input', () => {
      const result = renderMarkdown('<b>raw html bold</b>');
      expect(result).not.toContain('<b>raw html bold</b>');
    });
  });

  describe('Return type', () => {
    it('always returns a string (never undefined or null)', () => {
      const result = renderMarkdown('any content');
      expect(typeof result).toBe('string');
    });

    it('returns a string even for empty input', () => {
      const result = renderMarkdown('');
      expect(typeof result).toBe('string');
    });

    it('returns a string even for HTML injection attempts', () => {
      const result = renderMarkdown('<script>alert("xss")</script>');
      expect(typeof result).toBe('string');
    });
  });
});
