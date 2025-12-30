import { ColumnDef, SortDirection } from "@tanstack/react-table";

export interface DataTableColumnMeta {
	title?: string;
	initial?: {
		visible?: boolean;
		sort?: SortDirection;
	};
	alwaysVisible?: boolean;
}

export type DataTableColumnDef<TData, TValue = unknown> = ColumnDef<TData, TValue> & {
	meta?: DataTableColumnMeta;
};

export function getDataTableColumnMeta(columnDef: ColumnDef<any, any>) {
	return columnDef.meta as DataTableColumnMeta | undefined;
}

export function getColumnTitle<TData, TValue>(columnDef: ColumnDef<TData, TValue>) {
	const meta = getDataTableColumnMeta(columnDef);
	if (meta?.title) {
		return meta.title;
	}
	if (typeof columnDef.header === "string") {
		return columnDef.header;
	}
	return "";
}
