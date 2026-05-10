import React from 'react';
import { render, screen } from '@testing-library/react';
import { describe, it, expect } from 'vitest';
import { ToolPreviewSection } from '../mcp/ToolPreviewSection';
import { ToolPreviewDto } from '../../types';

const TOOLS: ToolPreviewDto[] = [
  { name: 'search-web', description: 'Searches the web' },
  { name: 'read-file', description: null },
];

describe('ToolPreviewSection', () => {
  it('Scenario1_ShowsToolCount', () => {
    render(<ToolPreviewSection tools={TOOLS} />);
    expect(screen.getByText(/2 tools found/i)).toBeInTheDocument();
  });

  it('Scenario1_ShowsToolNames', () => {
    render(<ToolPreviewSection tools={TOOLS} />);
    expect(screen.getByText('search-web')).toBeInTheDocument();
    expect(screen.getByText('read-file')).toBeInTheDocument();
  });

  it('Scenario1_ShowsToolDescription', () => {
    render(<ToolPreviewSection tools={TOOLS} />);
    expect(screen.getByText('Searches the web')).toBeInTheDocument();
  });

  it('Scenario1_IsReadOnly_NoCheckboxes', () => {
    render(<ToolPreviewSection tools={TOOLS} />);
    expect(screen.queryByRole('checkbox')).not.toBeInTheDocument();
  });

  it('Scenario2_ZeroTools_ShowsNoToolsMessage', () => {
    render(<ToolPreviewSection tools={[]} />);
    expect(screen.getByText(/no tools found on this server/i)).toBeInTheDocument();
  });

  it('Scenario2_ZeroTools_SectionStillVisible', () => {
    render(<ToolPreviewSection tools={[]} />);
    expect(screen.getByRole('list')).toBeInTheDocument();
  });
});
