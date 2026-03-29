import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

import { DataTable, type ColumnDef } from './DataTable';

interface TestRow {
  id: string;
  name: string;
  age: number;
}

const columns: ColumnDef<TestRow>[] = [
  { key: 'name', header: 'Name', render: (row) => row.name },
  { key: 'age', header: 'Age', render: (row) => String(row.age) },
];

const testData: TestRow[] = [
  { id: '1', name: 'Alice', age: 30 },
  { id: '2', name: 'Bob', age: 25 },
  { id: '3', name: 'Charlie', age: 35 },
];

const defaultProps = {
  columns,
  data: testData,
  totalCount: 3,
  page: 1,
  pageSize: 20,
  onPageChange: vi.fn(),
};

describe('DataTable', () => {
  it('should render column headers', () => {
    render(<DataTable {...defaultProps} />);

    expect(screen.getByText('Name')).toBeInTheDocument();
    expect(screen.getByText('Age')).toBeInTheDocument();
  });

  it('should render data rows', () => {
    render(<DataTable {...defaultProps} />);

    expect(screen.getByText('Alice')).toBeInTheDocument();
    expect(screen.getByText('Bob')).toBeInTheDocument();
    expect(screen.getByText('Charlie')).toBeInTheDocument();
    expect(screen.getByText('30')).toBeInTheDocument();
  });

  it('should show empty message when data is empty', () => {
    render(<DataTable {...defaultProps} data={[]} totalCount={0} />);

    expect(screen.getByText('lockey_common_no_results')).toBeInTheDocument();
  });

  it('should show custom empty message', () => {
    render(
      <DataTable
        {...defaultProps}
        data={[]}
        totalCount={0}
        emptyMessage="No records found"
      />,
    );

    expect(screen.getByText('No records found')).toBeInTheDocument();
  });

  it('should render loading state with skeletons', () => {
    render(<DataTable {...defaultProps} isLoading />);

    expect(screen.getByRole('status')).toBeInTheDocument();
    expect(screen.queryByText('Alice')).not.toBeInTheDocument();
  });

  it('should call onRowClick when row is clicked', async () => {
    const user = userEvent.setup();
    const onRowClick = vi.fn();

    render(<DataTable {...defaultProps} onRowClick={onRowClick} />);

    await user.click(screen.getByText('Alice'));

    expect(onRowClick).toHaveBeenCalledWith(testData[0]);
  });

  it('should render pagination when totalPages is greater than 1', () => {
    render(<DataTable {...defaultProps} totalCount={100} pageSize={20} page={1} />);

    expect(screen.getByText('lockey_common_previous')).toBeInTheDocument();
    expect(screen.getByText('lockey_common_next')).toBeInTheDocument();
  });

  it('should not render pagination when data fits on one page', () => {
    render(<DataTable {...defaultProps} totalCount={3} pageSize={20} />);

    expect(screen.queryByText('lockey_common_previous')).not.toBeInTheDocument();
    expect(screen.queryByText('lockey_common_next')).not.toBeInTheDocument();
  });

  it('should disable previous button on first page', () => {
    render(<DataTable {...defaultProps} totalCount={100} pageSize={20} page={1} />);

    expect(screen.getByText('lockey_common_previous')).toBeDisabled();
  });

  it('should disable next button on last page', () => {
    render(<DataTable {...defaultProps} totalCount={100} pageSize={20} page={5} />);

    expect(screen.getByText('lockey_common_next')).toBeDisabled();
  });

  it('should call onPageChange with next page', async () => {
    const user = userEvent.setup();
    const onPageChange = vi.fn();

    render(
      <DataTable
        {...defaultProps}
        totalCount={100}
        pageSize={20}
        page={2}
        onPageChange={onPageChange}
      />,
    );

    await user.click(screen.getByText('lockey_common_next'));

    expect(onPageChange).toHaveBeenCalledWith(3);
  });

  it('should call onPageChange with previous page', async () => {
    const user = userEvent.setup();
    const onPageChange = vi.fn();

    render(
      <DataTable
        {...defaultProps}
        totalCount={100}
        pageSize={20}
        page={3}
        onPageChange={onPageChange}
      />,
    );

    await user.click(screen.getByText('lockey_common_previous'));

    expect(onPageChange).toHaveBeenCalledWith(2);
  });

  it('should render page size selector when onPageSizeChange is provided', () => {
    const onPageSizeChange = vi.fn();

    render(
      <DataTable
        {...defaultProps}
        totalCount={100}
        pageSize={20}
        onPageSizeChange={onPageSizeChange}
      />,
    );

    expect(screen.getByText('lockey_common_items_per_page')).toBeInTheDocument();
    expect(screen.getByRole('combobox')).toBeInTheDocument();
  });

  it('should call onPageSizeChange when page size is changed', async () => {
    const user = userEvent.setup();
    const onPageSizeChange = vi.fn();

    render(
      <DataTable
        {...defaultProps}
        totalCount={100}
        pageSize={20}
        onPageSizeChange={onPageSizeChange}
      />,
    );

    await user.selectOptions(screen.getByRole('combobox'), '50');

    expect(onPageSizeChange).toHaveBeenCalledWith(50);
  });
});
