import { ConfigureBackupLocation } from "@/components/ConfigureBackupLocation";
import { ConfigureDb } from "@/components/ConfigureDb";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardFooter, CardHeader, CardTitle } from "@/components/ui/card";
import { Combobox } from "@/components/ui/combobox";
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle, DialogTrigger } from "@/components/ui/dialog";
import { Label } from "@/components/ui/label";
import { ApiTypes } from "@/gen/api";
import { queries, useDeleteBackupLocation, useDeleteDatabase, usePutSettingsTimezone } from "@/hooks/useApi";
import { useServerMessages } from "@/stores/serverMessages";
import { makeUuid } from "@/utils/make-uuid";
import { useQuery, useSuspenseQuery } from "@tanstack/react-query";
import { Link, createFileRoute } from "@tanstack/react-router";
import { AlertTriangleIcon, EyeIcon, Loader2Icon, PlusIcon } from "lucide-react";
import { Suspense, useState } from "react";
import { useForm } from "react-hook-form";

export const Route = createFileRoute("/")({
	component: Page,
});

function TimeZonePicker() {
	const timezonesQuery = useQuery(queries.getTimezones());
	const [timeZone, setTimeZone] = useState<string | null>(null);

	const updateTimeZone = usePutSettingsTimezone();
	return (
		<div className="flex justify-center">
			<div className="w-[400px] flex flex-col gap-2 items-start">
				<Label className="text-pretty text-2xl">Select Timezone</Label>
				<Combobox
					options={timezonesQuery.data?.timeZones.map((timeZone) => ({ value: timeZone, label: timeZone })) ?? []}
					value={timeZone}
					onChange={setTimeZone}
				/>
				<Button
					disabled={timeZone === null}
					pending={updateTimeZone.isPending}
					onClick={() => {
						if (timeZone) {
							updateTimeZone.mutate({ timeZone });
						}
					}}
				>
					Save
				</Button>
			</div>
		</div>
	);
}

function Page() {
	return (
		<Suspense fallback={<Loader2Icon className="animate-spin" />}>
			<PageContent />
		</Suspense>
	);
}

function BackupLocations() {
	const backupLocationsQuery = useSuspenseQuery(queries.getBackupLocations());
	const backupLocations = backupLocationsQuery.data ?? {};
	const s3Ids = Object.keys(backupLocations);
	const [editConfig, setEditConfig] = useState<{ id: string; s3Config: ApiTypes["S3BackupLocation"] } | null>(null);
	const deleteLocation = useDeleteBackupLocation();

	return (
		<>
			<Dialog
				open={!!editConfig}
				onOpenChange={(nowOpen) => {
					if (!nowOpen) {
						setEditConfig(null);
					}
				}}
			>
				<DialogContent>
					<DialogHeader>
						<DialogTitle className="text-2xl">Backup Location</DialogTitle>
					</DialogHeader>
					{editConfig && (
						<ConfigureBackupLocation s3Config={editConfig.s3Config} id={editConfig.id} onSave={() => setEditConfig(null)} />
					)}
				</DialogContent>
			</Dialog>
			<div className="flex justify-between items-center">
				<h1 className="text-3xl font-bold">Backup Locations</h1>
				<Button
					onClick={() =>
						setEditConfig({
							id: makeUuid(),
							s3Config: {
								friendlyName: "",
								endpoint: "",
								bucket: "",
								prefix: "",
								forcePathStyle: false,
								accessKey: "",
								secretKey: "",
								id: makeUuid(),
								s3Path: "",
							},
						})
					}
				>
					<PlusIcon className="size-4" />
					Add Backup Location
				</Button>
			</div>
			<div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-2">
				{s3Ids.map((s3Id) => {
					const s3Config = backupLocations[s3Id];
					return (
						<Card key={s3Id}>
							<CardHeader>
								<CardTitle className="text-2xl">{s3Config.friendlyName}</CardTitle>
							</CardHeader>
							<CardContent className="grid grid-cols-[auto_1fr] gap-2">
								<p>Endpoint:</p>
								<p className="text-ellipsis overflow-hidden font-bold">{s3Config.endpoint}</p>
								<p>Bucket:</p>
								<p className="text-ellipsis overflow-hidden font-bold">{s3Config.bucket}</p>
								<p>Prefix:</p>
								<p className="text-ellipsis overflow-hidden font-bold">{s3Config.prefix}</p>
							</CardContent>
							<CardFooter className="flex gap-2 justify-end">
								<Button onClick={() => setEditConfig({ id: s3Id, s3Config })}>Edit</Button>
								<Button
									pending={deleteLocation.isPending}
									variant="destructive"
									onClick={() => deleteLocation.mutate({ id: s3Id })}
								>
									Delete
								</Button>
							</CardFooter>
						</Card>
					);
				})}
				{s3Ids.length === 0 && <p>No backup locations found</p>}
			</div>
		</>
	);
}

function Databases() {
	const databasesQuery = useSuspenseQuery(queries.getDatabases());
	const databases = databasesQuery.data ?? [];
	const deleteDatabase = useDeleteDatabase();
	const [deleteDialogOpen, setDeleteDialogOpen] = useState(false);
	const [editDb, setEditDb] = useState<{ db: ApiTypes["DbConfig"]; dbId: string } | null>(null);
	const { notifications } = useServerMessages();

	const { handleSubmit } = useForm<{}>({});
	return (
		<>
			<div className="flex justify-between items-center">
				<h1 className="text-2xl font-bold">Databases</h1>
				<Button
					onClick={() =>
						setEditDb({
							dbId: makeUuid(),
							db: {
								name: "",
								host: "",
								database: "",
								user: "fawkes_replication",
								password: "",
								port: "5432",
								useSsl: false,
							},
						})
					}
				>
					<PlusIcon className="size-4" />
					Add Database
				</Button>
			</div>
			<div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-2">
				{databases.map((database) => {
					const dbId = database.id;
					if (!dbId) return null;
					const hasNotifications = notifications.filter((notification) => notification.dbId === dbId).length > 0;
					return (
						<Card key={dbId}>
							<CardHeader>
								<div className="flex items-center justify-between">
									<CardTitle>{database.db?.name ?? ""}</CardTitle>
									{hasNotifications && (
										<div>
											<AlertTriangleIcon className="text-yellow-500" />
										</div>
									)}
								</div>
							</CardHeader>
							<CardContent className="grid grid-cols-[auto_1fr] gap-2">
								<p>Host:</p>
								<p className="text-ellipsis overflow-hidden font-bold">{database.db?.host ?? ""}</p>
								<p>Database:</p>
								<p className="text-ellipsis overflow-hidden font-bold">{database.db?.database ?? ""}</p>
							</CardContent>
							<CardFooter className="flex gap-2 justify-between">
								<Link to="/db" search={{ dbId }}>
									<Button size="icon">
										<EyeIcon />
									</Button>
								</Link>
								<Dialog
									open={deleteDialogOpen}
									onOpenChange={(nowOpen) => {
										setDeleteDialogOpen(nowOpen);
									}}
								>
									<DialogTrigger>
										<Button type="button" variant="destructive">
											Remove
										</Button>
									</DialogTrigger>
									<DialogContent>
										<form
											onSubmit={handleSubmit(({}) => {
												deleteDatabase.mutate(
													{ dbId },
													{
														onSuccess: () => {
															setDeleteDialogOpen(false);
														},
													}
												);
											})}
											className="flex flex-col gap-4"
										>
											<DialogHeader>
												<DialogTitle>Remove Database</DialogTitle>
												<DialogDescription>
													This will not delete your backups. It will just stop the backup process.
												</DialogDescription>
											</DialogHeader>
											<DialogFooter>
												<Button variant="destructive" pending={deleteDatabase.isPending} type="submit">
													Remove
												</Button>
											</DialogFooter>
										</form>
									</DialogContent>
								</Dialog>
							</CardFooter>
						</Card>
					);
				})}
			</div>
			<Dialog
				open={!!editDb}
				onOpenChange={(nowOpen) => {
					if (!nowOpen) {
						setEditDb(null);
					}
				}}
			>
				<DialogContent>
					<DialogHeader>
						<DialogTitle>Edit Database</DialogTitle>
					</DialogHeader>
					{editDb && <ConfigureDb db={editDb.db} onSave={() => setEditDb(null)} dbId={editDb.dbId} />}
				</DialogContent>
			</Dialog>
		</>
	);
}

function PageContent() {
	const timeZoneQuery = useSuspenseQuery(queries.getSavedTimezone());

	if (timeZoneQuery.data?.timeZoneName === null) {
		return <TimeZonePicker />;
	}

	return (
		<div className="flex flex-col gap-2">
			<BackupLocations />
			<Databases />
		</div>
	);
}
