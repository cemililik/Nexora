import { describe, expect, it } from 'vitest';
import { render, screen } from '@testing-library/react';

import { EntityDiffViewer } from './EntityDiffViewer';

describe('EntityDiffViewer', () => {
  it('should render diff table with valid changes', () => {
    const changes = JSON.stringify([
      { field: 'name', old: 'Alice', new: 'Bob' },
      { field: 'email', old: 'a@b.com', new: 'b@b.com' },
    ]);

    render(<EntityDiffViewer changes={changes} />);

    expect(screen.getByText('lockey_audit_col_field')).toBeInTheDocument();
    expect(screen.getByText('lockey_audit_col_old_value')).toBeInTheDocument();
    expect(screen.getByText('lockey_audit_col_new_value')).toBeInTheDocument();
    expect(screen.getByText('name')).toBeInTheDocument();
    expect(screen.getByText('Alice')).toBeInTheDocument();
    expect(screen.getByText('Bob')).toBeInTheDocument();
    expect(screen.getByText('email')).toBeInTheDocument();
  });

  it('should render no-changes message when changes is null', () => {
    render(<EntityDiffViewer changes={null} />);

    expect(screen.getByText('lockey_audit_detail_no_changes')).toBeInTheDocument();
  });

  it('should render no-changes message when changes is undefined', () => {
    render(<EntityDiffViewer changes={undefined} />);

    expect(screen.getByText('lockey_audit_detail_no_changes')).toBeInTheDocument();
  });

  it('should render no-changes message when changes is empty string', () => {
    render(<EntityDiffViewer changes="" />);

    expect(screen.getByText('lockey_audit_detail_no_changes')).toBeInTheDocument();
  });

  it('should render no-changes message for malformed JSON', () => {
    render(<EntityDiffViewer changes="not valid json" />);

    expect(screen.getByText('lockey_audit_detail_no_changes')).toBeInTheDocument();
  });

  it('should render no-changes message when JSON is not an array', () => {
    render(<EntityDiffViewer changes='{"field":"name"}' />);

    expect(screen.getByText('lockey_audit_detail_no_changes')).toBeInTheDocument();
  });

  it('should filter out invalid entries from changes array', () => {
    const changes = JSON.stringify([
      { field: 'name', old: 'Alice', new: 'Bob' },
      { invalid: true },
      null,
      'string',
    ]);

    render(<EntityDiffViewer changes={changes} />);

    expect(screen.getByText('name')).toBeInTheDocument();
    expect(screen.getByText('Alice')).toBeInTheDocument();
    expect(screen.getByText('Bob')).toBeInTheDocument();
  });

  it('should render dash for null old value', () => {
    const changes = JSON.stringify([
      { field: 'status', old: null, new: 'Active' },
    ]);

    render(<EntityDiffViewer changes={changes} />);

    expect(screen.getByText('status')).toBeInTheDocument();
    // The em-dash is rendered for null values
    const cells = screen.getAllByRole('cell');
    expect(cells).toHaveLength(3);
    const oldValueCell = cells[1];
    expect(oldValueCell).toBeDefined();
    expect(oldValueCell!.textContent).toBe('\u2014');
    expect(screen.getByText('Active')).toBeInTheDocument();
  });

  it('should render dash for null new value', () => {
    const changes = JSON.stringify([
      { field: 'notes', old: 'Some text', new: null },
    ]);

    render(<EntityDiffViewer changes={changes} />);

    expect(screen.getByText('Some text')).toBeInTheDocument();
    const cells = screen.getAllByRole('cell');
    expect(cells).toHaveLength(3);
    const newValueCell = cells[2];
    expect(newValueCell).toBeDefined();
    expect(newValueCell!.textContent).toBe('\u2014');
  });
});
