import React from 'react';
import { render, screen } from '@testing-library/react';
import { describe, it, expect } from 'vitest';
import McpTransportBadge from '../mcp/McpTransportBadge';

describe('McpTransportBadge', () => {
  it('renders_http_label_when_transport_type_is_http', () => {
    render(<McpTransportBadge transportType="HTTP" />);

    expect(screen.getByText('HTTP')).toBeInTheDocument();
  });

  it('renders_stdio_label_when_transport_type_is_stdio', () => {
    render(<McpTransportBadge transportType="STDIO" />);

    expect(screen.getByText('stdio')).toBeInTheDocument();
  });

  it('has_aria_label_for_http_transport', () => {
    render(<McpTransportBadge transportType="HTTP" />);

    expect(screen.getByLabelText('Transport: HTTP')).toBeInTheDocument();
  });

  it('has_aria_label_for_stdio_transport', () => {
    render(<McpTransportBadge transportType="STDIO" />);

    expect(screen.getByLabelText('Transport: stdio')).toBeInTheDocument();
  });
});
