"use client";

import {
	ColumnFiltersState,
	PaginationState,
	Row,
	SortingState,
	TableMeta,
	VisibilityState,
	flexRender,
	getCoreRowModel,
	getFilteredRowModel,
	getPaginationRowModel,
	getSortedRowModel,
	useReactTable,
} from "@tanstack/react-table";

import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { Loader2Icon } from "lucide-react";
import { useState } from "react";
import { Checkbox } from "../checkbox";
import { Label } from "../label";
import { DataTableColumnDef } from "./column-def";
import { DataTableColumnHeader } from "./column-header";
import { DataTablePagination } from "./pagination";

interface DataTableProps<TData, TValue> {
	columns: DataTableColumnDef<TData, TValue>[];
	data: TData[];
	meta?: TableMeta<TData>;
	loading?: boolean;
	rowSelect?: boolean;
	expandedRowContent?: (row: Row<TData>) => React.ReactNode;
	getRowId?: (row: TData) => string;
}

function getColId<TData, TValue>(column: DataTableColumnDef<TData, TValue>): string | null {
	if (column.id) {
		return column.id;
	}
	if ("accessorKey" in column) {
		return column.accessorKey.toString();
	}
	return null;
}

function getColumnVisibility<TData, TValue>(columns: DataTableColumnDef<TData, TValue>[]): VisibilityState {
	const visibility: VisibilityState = {};
	for (const column of columns) {
		const id = getColId(column);
		if (id && !(column.meta?.initial?.visible ?? true)) {
			visibility[id] = false;
		}
	}
	return visibility;
}

function getInitialSort<TData, TValue>(columns: DataTableColumnDef<TData, TValue>[]): SortingState {
	const sorting: SortingState = [];
	for (const column of columns) {
		const id = getColId(column);
		if (id && column.meta?.initial?.sort) {
			sorting.push({ desc: column.meta.initial.sort === "desc", id });
		}
	}
	return sorting;
}

function AddRowSelectColumn<TData, TValue>(columns: DataTableColumnDef<TData, TValue>[]) {
	return [
		{
			id: "rowSelect",
			header: ({ table }) => (
				<Checkbox
					checked={table.getIsAllPageRowsSelected() || (table.getIsSomePageRowsSelected() && "indeterminate")}
					onCheckedChange={(value) => table.toggleAllPageRowsSelected(!!value)}
					aria-label="Select all"
				/>
			),
			cell: ({ row }) => (
				<Checkbox checked={row.getIsSelected()} onCheckedChange={(value) => row.toggleSelected(!!value)} aria-label="Select row" />
			),
			meta: {
				alwaysVisible: true,
			},
		},
		...columns,
	] as DataTableColumnDef<TData, TValue>[];
}

export function DataTable<TData, TValue>({
	columns,
	data,
	meta,
	loading = false,
	rowSelect,
	expandedRowContent,
	getRowId,
}: DataTableProps<TData, TValue>) {
	const [sorting, setSorting] = useState<SortingState>(() => getInitialSort(columns));
	const [columnVisibility, setColumnVisibility] = useState<VisibilityState>(() => getColumnVisibility(columns));
	const [columnFilters, setColumnFilters] = useState<ColumnFiltersState>([]);
	const [pagination, setPagination] = useState<PaginationState>({ pageIndex: 0, pageSize: 10 });

	const table = useReactTable({
		data,
		columns: rowSelect ? AddRowSelectColumn(columns) : columns,
		getCoreRowModel: getCoreRowModel(),
		meta,
		getPaginationRowModel: getPaginationRowModel(),
		onSortingChange: setSorting,
		getSortedRowModel: getSortedRowModel(),
		onColumnVisibilityChange: setColumnVisibility,
		onColumnFiltersChange: setColumnFilters,
		getFilteredRowModel: getFilteredRowModel(),
		onPaginationChange: setPagination,
		state: {
			sorting,
			columnVisibility,
			columnFilters,
			pagination: pagination,
		},
		getRowId,
	});

	return (
		<div className="flex flex-col gap-2">
			<div className="rounded-md border">
				<Table>
					<TableHeader>
						{table.getHeaderGroups().map((headerGroup) => (
							<TableRow key={headerGroup.id}>
								{headerGroup.headers.map((header) => {
									if (header.column.getCanSort() || header.column.getCanFilter()) {
										return (
											<TableHead key={header.id}>
												<DataTableColumnHeader column={header.column} />
											</TableHead>
										);
									}
									return (
										<TableHead key={header.id}>
											{header.isPlaceholder ? null : flexRender(header.column.columnDef.header, header.getContext())}
										</TableHead>
									);
								})}
							</TableRow>
						))}
					</TableHeader>
					<TableBody>
						{table.getRowModel().rows?.length ? (
							table.getRowModel().rows.map((row) => (
								<>
									<TableRow key={row.id} data-state={row.getIsSelected() && "selected"}>
										{row.getVisibleCells().map((cell) => (
											<TableCell key={cell.id}>{flexRender(cell.column.columnDef.cell, cell.getContext())}</TableCell>
										))}
									</TableRow>
									{row.getIsExpanded() && expandedRowContent && expandedRowContent(row)}
								</>
							))
						) : (
							<TableRow>
								<TableCell colSpan={columns.length} className="h-24 text-center">
									{loading ? (
										<div className="flex gap-2 items-center justify-center">
											<Loader2Icon className="animate-spin" />
											<Label>{"Loading..."}</Label>
										</div>
									) : (
										<Label>{"No results."}</Label>
									)}
								</TableCell>
							</TableRow>
						)}
					</TableBody>
				</Table>
			</div>
			<DataTablePagination table={table} rowSelect={rowSelect ?? false} />
		</div>
	);
}
