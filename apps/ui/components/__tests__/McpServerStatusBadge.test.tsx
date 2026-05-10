import React from 'react';
import { render, screen } from '@testing-library/react';
import { describe, it, expect } from 'vitest';
import McpServerStatusBadge from '../mcp/McpServerStatusBadge';

describe('McpServerStatusBadge', () => {

  describe('Connected status', () => {
    it('renders_connected_label', () => {
      render(<McpServerStatusBadge status="Connected" />);
      expect(screen.getByText('Connected')).toBeInTheDocument();
    });

    it('has_accessible_aria_label_for_connected', () => {
      render(<McpServerStatusBadge status="Connected" />);
      expect(
        screen.getByLabelText('Connection status: Connected')
      ).toBeInTheDocument();
    });

    it('renders_dot_indicator_for_connected', () => {
      const { container } = render(<McpServerStatusBadge status="Connected" />);
      const dot = container.querySelector('[aria-hidden="true"]');
      expect(dot).toBeInTheDocument();
      expect(dot?.className).toContain('bg-emerald-500');
    });
  });

  describe('ConnectionFailed status', () => {
    it('renders_connection_failed_label', () => {
      render(<McpServerStatusBadge status="ConnectionFailed" />);
      expect(screen.getByText('Connection Failed')).toBeInTheDocument();
    });

    it('has_accessible_aria_label_for_connection_failed', () => {
      render(<McpServerStatusBadge status="ConnectionFailed" />);
      expect(
        screen.getByLabelText('Connection status: Connection Failed')
      ).toBeInTheDocument();
    });

    it('renders_dot_indicator_for_connection_failed', () => {
      const { container } = render(<McpServerStatusBadge status="ConnectionFailed" />);
      const dot = container.querySelector('[aria-hidden="true"]');
      expect(dot).toBeInTheDocument();
      expect(dot?.className).toContain('bg-red-500');
    });
  });

  describe('Unverified status', () => {
    it('renders_unverified_label', () => {
      render(<McpServerStatusBadge status="Unverified" />);
      expect(screen.getByText('Unverified')).toBeInTheDocument();
    });

    it('has_accessible_aria_label_for_unverified', () => {
      render(<McpServerStatusBadge status="Unverified" />);
      expect(
        screen.getByLabelText('Connection status: Unverified')
      ).toBeInTheDocument();
    });

    it('renders_dot_indicator_for_unverified', () => {
      const { container } = render(<McpServerStatusBadge status="Unverified" />);
      const dot = container.querySelector('[aria-hidden="true"]');
      expect(dot).toBeInTheDocument();
      expect(dot?.className).toContain('bg-zinc-500');
    });
  });

});
