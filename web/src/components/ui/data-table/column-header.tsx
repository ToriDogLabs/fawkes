import { cn } from "@/utils/css";
import { ArrowDownIcon, ArrowUpIcon, CaretSortIcon } from "@radix-ui/react-icons";
import { Column } from "@tanstack/react-table";
import { SearchIcon } from "lucide-react";
import { Button } from "../button";
import { Input } from "../input";
import { Label } from "../label";
import { Popover, PopoverContent, PopoverTrigger } from "../popover";
import { getColumnTitle } from "./column-def";

interface DataTableColumnHeaderProps<TData, TValue> extends React.HTMLAttributes<HTMLDivElement> {
	column: Column<TData, TValue>;
}

export function DataTableColumnHeader<TData, TValue>({ column, className }: DataTableColumnHeaderProps<TData, TValue>) {
	const showSort = column.getCanSort();
	const showFilter = column.getCanFilter();

	return (
		<div className={cn("flex items-center space-x-1", className)}>
			<div className="flex space-x-2 items-center">
				<Label>{getColumnTitle(column.columnDef)}</Label>
				{showSort && (
					<Button
						variant="ghost"
						size="icon"
						className="h-8 data-[state=open]:bg-accent"
						onClick={() => {
							column.toggleSorting();
						}}
					>
						{column.getIsSorted() === "desc" ? (
							<ArrowDownIcon className="size-4" />
						) : column.getIsSorted() === "asc" ? (
							<ArrowUpIcon className="size-4" />
						) : (
							<CaretSortIcon className="size-4" />
						)}
					</Button>
				)}
			</div>
			{showFilter && (
				<Popover>
					<PopoverTrigger asChild>
						<Button variant="ghost" size="icon">
							<SearchIcon className="size-4" />
						</Button>
					</PopoverTrigger>
					<PopoverContent align="start" className="flex flex-col gap-2">
						{showFilter && (
							<Input
								placeholder="Filter"
								value={(column.getFilterValue() as string) ?? ""}
								onChange={(event) => column.setFilterValue(event.target.value)}
								className="max-w-sm"
							/>
						)}
					</PopoverContent>
				</Popover>
			)}
		</div>
	);
}
