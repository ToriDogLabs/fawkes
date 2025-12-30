import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardFooter, CardHeader, CardTitle } from "@/components/ui/card";
import { DataTableColumnDef } from "@/components/ui/data-table/column-def";
import { DataTable } from "@/components/ui/data-table/table";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import {
	queries,
	useDeleteSchedule,
	useDeleteServerBackupid,
	useKeepBackup,
	usePostBackup,
	usePutRetention,
	usePutSchedule,
	useSaveDbBackupLocations,
} from "@/hooks/useApi";

import { ApiTypes } from "@/gen/api";
import { useServerMessages } from "@/stores/serverMessages";
import { cn } from "@/utils/css";
import { makeUuid } from "@/utils/make-uuid";
import { ReQuartzCron, Tab } from "@sbzen/re-cron";
import { useQuery, useSuspenseQuery } from "@tanstack/react-query";
import { ReactNode } from "@tanstack/react-router";
import cronstrue from "cronstrue";
import {
	ArchiveIcon,
	ChevronDownIcon,
	ChevronRightIcon,
	HistoryIcon,
	Loader2Icon,
	LockIcon,
	SettingsIcon,
	Trash2Icon,
	UnlockIcon,
} from "lucide-react";
import { Fragment, Suspense, useMemo, useState } from "react";
import { useForm } from "react-hook-form";
import { ArchivalCard } from "./ArchivalCard";
import { RecoverContent } from "./RecoverContent";
import { Alert, AlertTitle } from "./ui/alert";
import { Checkbox } from "./ui/checkbox";
import { Dialog, DialogContent, DialogFooter, DialogHeader, DialogTitle, DialogTrigger } from "./ui/dialog";
import { TableCell, TableRow } from "./ui/table";
import { Tooltip, TooltipContent, TooltipTrigger } from "./ui/tooltip";

function RetentionCard({ policy, dbId }: { policy: ApiTypes["RetentionPolicy"]; dbId: string }) {
	const updateRetentionPolicy = usePutRetention();

	const { register, handleSubmit, watch, setValue } = useForm<ApiTypes["RetentionPolicy"]>({ values: policy });

	const units = watch("retentionUnits");

	function updatePolicy(policy: ApiTypes["RetentionPolicy"]) {
		updateRetentionPolicy.mutate({ dbId, policy });
	}
	return (
		<Card>
			<CardHeader>
				<CardTitle>Basic Retention Policy</CardTitle>
				<CardDescription>This policy will be used to retain backups for the database.</CardDescription>
			</CardHeader>
			<form onSubmit={handleSubmit(updatePolicy)}>
				<CardContent className="flex flex-col gap-2 items-start">
					<Label htmlFor="minimumRedundancy">Minimum Redundancy</Label>
					<Input id="minimumRedundancy" {...register("minimumRedundancy")} type="number" />
					<Label>Recovery Window</Label>
					<div className="flex gap-2 items-center">
						<Input {...register("retention")} type="number" className="w-20" />
						<Select
							value={units}
							onValueChange={(e) => setValue("retentionUnits", e as ApiTypes["RetentionPolicy"]["retentionUnits"])}
						>
							<SelectTrigger>
								<SelectValue />
							</SelectTrigger>
							<SelectContent side="top">
								<SelectItem value="DAYS">Days</SelectItem>
								<SelectItem value="WEEKS">Weeks</SelectItem>
								<SelectItem value="MONTHS">Months</SelectItem>
							</SelectContent>
						</Select>
					</div>
				</CardContent>
				<CardFooter>
					<Button type="submit" pending={updateRetentionPolicy.isPending}>
						Update
					</Button>
				</CardFooter>
			</form>
		</Card>
	);
}

function getCronString(cron: string) {
	try {
		return cronstrue.toString(cron, { verbose: true, dayOfWeekStartIndexZero: false });
	} catch (e) {}
	return "";
}

function CronLabel({ schedule }: { schedule: ApiTypes["DbBackupSchedule"] }) {
	return <>{`${schedule.type === "full" ? "Full" : "Incremental"} Backup ${getCronString(schedule.cron)}`}</>;
}

function CronCard({ schedules, dbId }: { schedules: ApiTypes["DbBackupSchedule"][]; dbId: string }) {
	const updateSchedule = usePutSchedule();
	const deleteSchedule = useDeleteSchedule();
	const [editing, setEditing] = useState<ApiTypes["DbBackupSchedule"] | null>(null);

	return (
		<Card>
			<CardHeader>
				<CardTitle>Backup Schedule</CardTitle>
			</CardHeader>
			{editing === null && (
				<CardContent className="flex gap-2 flex-col pl-12" key="non-editing">
					<ul className="list-disc">
						{schedules.map((schedule, index) => (
							<li key={schedule.id}>
								<div className="flex gap-2 items-center">
									<CronLabel schedule={schedule} />
									<Button variant="ghost" size="icon" onClick={() => setEditing({ ...schedules[index] })}>
										<SettingsIcon className="size-5" />
									</Button>
									<Button
										variant="ghost"
										size="icon"
										onClick={() => {
											deleteSchedule.mutate({ dbId, id: schedule.id });
										}}
									>
										<Trash2Icon className="size-5" />
									</Button>
								</div>
							</li>
						))}
					</ul>
				</CardContent>
			)}
			{editing !== null && (
				<>
					<CardContent className="my-cron">
						{editing && <CronLabel schedule={editing} />}
						<ReQuartzCron
							tabs={[Tab.HOURS, Tab.MINUTES, Tab.SECONDS, Tab.DAY, Tab.MONTH]}
							value={editing.cron}
							onChange={(v) => setEditing({ ...editing, cron: v })}
							cssClassPrefix="cron-"
						/>
					</CardContent>
					{editing && (
						<CardFooter className="flex gap-2">
							<Button
								pending={updateSchedule.isPending}
								disabled={editing.cron === undefined}
								onClick={() =>
									updateSchedule.mutate(
										{
											dbId: dbId,
											schedule: editing,
										},
										{
											onSuccess: () => setEditing(null),
										}
									)
								}
							>
								Save
							</Button>
							<Button variant="outline" onClick={() => setEditing(null)}>
								Cancel
							</Button>
						</CardFooter>
					)}
				</>
			)}

			{!editing && (
				<CardFooter>
					<Button
						onClick={() => {
							setEditing({ id: makeUuid(), type: "full", cron: "" });
						}}
					>
						Add Schedule
					</Button>
				</CardFooter>
			)}
		</Card>
	);
}

function KeepButton({ server, backups }: { server: string; backups: ApiTypes["BackupGroup"]["backups"] }) {
	const { keep: keepBackup, remove: removeKeep, pendingBackups } = useKeepBackup();
	const lockedBackups = backups.filter((backup) => backup.archivalStatus === "full" || backup.archivalStatus === "standalone");
	const backupId = backups[0]?.backupId;
	const allLocked = lockedBackups.length === backups.length;

	return (
		<Button
			size="icon"
			variant="ghost"
			pending={pendingBackups.includes(backupId)}
			onClick={() => {
				if (allLocked) {
					removeKeep.mutate({ dbId: server, backupId, locationIds: lockedBackups.map((backup) => backup.locationId) });
				} else {
					keepBackup.mutate({
						dbId: server,
						backupId,
						locationIds: backups.filter((b) => !lockedBackups.includes(b)).map((backup) => backup.locationId),
					});
				}
			}}
		>
			{!allLocked && <UnlockIcon className="size-5" />}
			{allLocked && <LockIcon className="size-5 stroke-blue-600 dark:stroke-blue-400" />}
		</Button>
	);
}

function DeleteButton({ dbId, backups }: { dbId: string; backups: ApiTypes["BackupGroup"]["backups"] }) {
	const deletable = backups.filter((backup) => backup.archivalStatus === "nokeep");

	const deleteBackup = useDeleteServerBackupid();
	return (
		<Button
			size="icon"
			variant="ghost"
			disabled={deletable.length === 0}
			onClick={() =>
				deleteBackup.mutate({ dbId, backupId: backups[0].backupId, locationIds: deletable.map((backup) => backup.locationId) })
			}
			pending={deleteBackup.isPending}
		>
			<Trash2Icon className="size-5 text-red-500" />
		</Button>
	);
}

function backupIdToLocalTime(time: string | null | undefined) {
	if (!time) return "";
	return new Date(time).toLocaleString();
}

function useColumns(dbId: string, s3Configs: Record<string, ApiTypes["S3BackupLocation"]>) {
	return useMemo(() => {
		const columns: DataTableColumnDef<ApiTypes["BackupGroup"]>[] = [
			{
				id: "expander",
				header: () => "Location",
				cell: ({ row }) => {
					if (row.original.backups.length === 1) {
						const s3Config: ApiTypes["S3BackupLocation"] | undefined = s3Configs[row.original.backups[0].locationId];
						return s3Config?.friendlyName ?? row.original.backups[0].locationId;
					}
					return (
						<Button variant="ghost" size="icon" onClick={() => row.toggleExpanded()}>
							{row.getIsExpanded() ? <ChevronDownIcon /> : <ChevronRightIcon />}
						</Button>
					);
				},
				meta: {
					alwaysVisible: true,
				},
			},
			{
				id: "time",
				header: "Time",
				accessorFn: (row) => backupIdToLocalTime(row.timestamp),
				sortingFn: (rowA, rowB) => (rowA.original.backupId < rowB.original.backupId ? -1 : 1),
				sortDescFirst: true,
				meta: {
					initial: {
						sort: "desc",
					},
				},
				enableSorting: true,
				enableColumnFilter: true,
			},
			{
				header: "Backup Id",
				accessorKey: "backupId",
				sortDescFirst: true,
				meta: {
					initial: {
						visible: false,
					},
				},
				enableSorting: true,
				enableColumnFilter: true,
			},
			{
				header: "Name",
				accessorKey: "name",
				sortDescFirst: true,
				enableSorting: true,
				enableColumnFilter: true,
			},
			{
				header: "",
				id: "actions",
				cell: ({ row }) => {
					const backup = row.original;
					return (
						<div className="flex gap-1">
							<Tooltip>
								<TooltipTrigger>
									<ArchiveIcon
										className={cn("size-5 m-2 dark:stroke-blue-400 stroke-blue-600", {
											invisible: row.original.archivedUntil === null,
										})}
									/>
								</TooltipTrigger>
								<TooltipContent>
									Archived until {new Date(row.original.archivedUntil ?? "").toLocaleDateString()}
								</TooltipContent>
							</Tooltip>
							<Dialog>
								<DialogTrigger>
									<Tooltip>
										<TooltipTrigger>
											<Button size="icon" variant="ghost">
												<HistoryIcon className="size-5" />
											</Button>
										</TooltipTrigger>
										<TooltipContent>Recover</TooltipContent>
									</Tooltip>
								</DialogTrigger>
								<DialogContent>
									<DialogHeader>
										<DialogTitle>Recover {dbId}</DialogTitle>
									</DialogHeader>
									<RecoverContent dbId={dbId} backupId={backup.backupId} s3Id={backup.backups[0].locationId} />
								</DialogContent>
							</Dialog>
							<Tooltip>
								<TooltipTrigger>
									<KeepButton server={dbId} backups={backup.backups} />
								</TooltipTrigger>
								<TooltipContent>Keep</TooltipContent>
							</Tooltip>
							<Tooltip>
								<TooltipTrigger>
									<DeleteButton dbId={dbId} backups={backup.backups} />
								</TooltipTrigger>
								<TooltipContent>Delete</TooltipContent>
							</Tooltip>
						</div>
					);
				},
				meta: {
					alwaysVisible: true,
				},
			},
		];
		return columns;
	}, [dbId]);
}

function BackupLocationPicker({ dbId }: { dbId: string }) {
	const backupLocationsQuery = useSuspenseQuery(queries.getBackupLocations());
	const backupLocations = backupLocationsQuery.data ?? {};
	const databases = useSuspenseQuery(queries.getDatabases());
	const [selectedLocations, setSelectedLocations] = useState(databases.data?.find((db) => db.id === dbId)?.db?.backupLocations ?? []);
	const saveDbBackupLocations = useSaveDbBackupLocations();
	return (
		<>
			<div className="grid grid-cols-[auto_1fr] gap-2 mb-8">
				{Object.entries(backupLocations).map(([s3Id, s3Config]) => {
					return (
						<Fragment key={s3Id}>
							<Checkbox
								id={s3Id}
								checked={selectedLocations.includes(s3Id)}
								onCheckedChange={(v) => {
									if (v) {
										setSelectedLocations([...selectedLocations, s3Id]);
									} else {
										setSelectedLocations(selectedLocations.filter((id) => id !== s3Id));
									}
								}}
							/>
							<Label htmlFor={s3Id}>{s3Config.friendlyName}</Label>
						</Fragment>
					);
				})}
			</div>
			<CardFooter>
				<Button
					pending={saveDbBackupLocations.isPending}
					onClick={() => saveDbBackupLocations.mutate({ dbId, s3Ids: selectedLocations })}
				>
					Save
				</Button>
			</CardFooter>
		</>
	);
}

function BackupLoactionsCard({ dbId }: { dbId: string }) {
	return (
		<Card>
			<CardHeader>
				<CardTitle>Backup To</CardTitle>
			</CardHeader>
			<CardContent>
				<Suspense fallback={<Loader2Icon className="animate-spin" />}>
					<BackupLocationPicker dbId={dbId} />
				</Suspense>
			</CardContent>
		</Card>
	);
}

function StartBackupDialog({ onStart, children }: { onStart: (data: { name: string }) => void; children: ReactNode }) {
	const [open, setOpen] = useState(false);
	const { register, handleSubmit, reset } = useForm<{ name: string }>({});
	return (
		<Dialog open={open} onOpenChange={setOpen}>
			{children}
			<DialogContent>
				<form
					onSubmit={handleSubmit((d) => {
						onStart(d);
						setOpen(false);
						reset();
					})}
					className="flex flex-col gap-4"
				>
					<DialogHeader>
						<DialogTitle>Configure Backup</DialogTitle>
					</DialogHeader>

					<Label htmlFor="name">Backup Name</Label>
					<Input id="name" {...register("name")} />

					<DialogFooter>
						<Button type="submit">Start Backup</Button>
					</DialogFooter>
				</form>
			</DialogContent>
		</Dialog>
	);
}

export function BackupsPage({ dbId }: { dbId: string }) {
	const backups = useQuery(queries.getServerBackups(dbId));
	const backupSchedulesQuery = useQuery(queries.getSchedule(dbId));
	const retentionPolicy = useQuery(queries.getRetention(dbId));
	const backupLocationsQuery = useSuspenseQuery(queries.getBackupLocations());
	const columns = useColumns(dbId, backupLocationsQuery.data ?? {});
	const messages = useServerMessages();

	const backupNow = usePostBackup();

	function startBackup(name?: string | null) {
		backupNow.mutate({ dbId, name });
	}
	return (
		<div className="flex flex-col gap-2">
			{messages &&
				messages.notifications
					.filter((notification) => notification.dbId === dbId)
					.map((notification) => (
						<Alert key={notification.id} variant={notification.variant === "Destructive" ? "destructive" : "default"}>
							<AlertTitle className="flex items-center gap-2">
								{notification.variant === "OnGoing" ? <Loader2Icon className="animate-spin" /> : undefined}
								{notification.message}
							</AlertTitle>
						</Alert>
					))}
			<BackupLoactionsCard dbId={dbId} />
			<Card>
				<CardHeader>
					<CardTitle className="flex items-center justify-between">
						<span className="flex-1">Backups</span>
						<Button type="submit" pending={backupNow.isPending} onClick={() => startBackup()} className="rounded-r-none">
							Create Backup
						</Button>
						<StartBackupDialog onStart={({ name }) => startBackup(name)}>
							<DialogTrigger asChild>
								<Button size="icon" className="rounded-l-none border-l border-border">
									<SettingsIcon />
								</Button>
							</DialogTrigger>
						</StartBackupDialog>
					</CardTitle>
				</CardHeader>
				<CardContent>
					<DataTable
						columns={columns}
						data={backups.data ?? []}
						loading={backups.isPending}
						rowSelect
						getRowId={(row) => row.backupId}
						expandedRowContent={(row) => (
							<>
								{row.original.backups.map((backup) => {
									const s3Config: ApiTypes["S3BackupLocation"] | undefined =
										backupLocationsQuery.data?.[backup.locationId];
									return (
										<TableRow key={backup.locationId}>
											<TableCell colSpan={1} />
											<TableCell colSpan={row.getVisibleCells().length - 2}>
												<p className="pl-3">{s3Config?.friendlyName ?? backup.locationId}</p>
											</TableCell>
											<TableCell>
												<div key={backups.dataUpdatedAt} className="flex gap-1">
													<Button size="icon" variant="ghost" className="invisible">
														<HistoryIcon className="size-5" />
													</Button>

													<Tooltip>
														<TooltipTrigger>
															<KeepButton server={dbId} backups={[backup]} />
														</TooltipTrigger>
														<TooltipContent>Keep</TooltipContent>
													</Tooltip>
													<Tooltip>
														<TooltipTrigger>
															<DeleteButton dbId={dbId} backups={[backup]} />
														</TooltipTrigger>
														<TooltipContent>Delete</TooltipContent>
													</Tooltip>
												</div>
											</TableCell>
										</TableRow>
									);
								})}
							</>
						)}
					/>
				</CardContent>
			</Card>
			{backupSchedulesQuery.data !== undefined && <CronCard schedules={backupSchedulesQuery.data ?? []} dbId={dbId} />}

			{retentionPolicy.data && <RetentionCard policy={retentionPolicy.data} dbId={dbId} />}

			<ArchivalCard dbId={dbId} />
		</div>
	);
}
