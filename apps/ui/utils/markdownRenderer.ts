import { marked, Renderer } from 'marked';

const customRenderer = new Renderer();
customRenderer.html = () => '';

marked.use({
  renderer: customRenderer,
  breaks: true,
  gfm: true,
});

export function renderMarkdown(input: string): string {
  if (!input || input.trim() === '') {
    return '';
  }

  try {
    return marked.parse(input) as string;
  } catch {
    return '';
  }
}
