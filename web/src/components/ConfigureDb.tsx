import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { ApiTypes } from "@/gen/api";
import { useSaveDb } from "@/hooks/useApi";
import { sqlHelpers } from "@/sqlHelpers";
import { zodResolver } from "@hookform/resolvers/zod";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { Button } from "./ui/button";
import { Checkbox } from "./ui/checkbox";
import { Textarea } from "./ui/textarea";

export const DbConfigSchema = z.object({
	name: z.string().trim().min(1, "Name is required"),
	host: z.string().trim().min(1, "Host is required"),
	database: z.string().trim().min(1, "Database is required"),
	user: z.string().trim().min(1, "User is required"),
	password: z.string().trim().min(1, "Password is required"),
	port: z.string().trim().min(1, "Port is required"),
	useSsl: z.boolean(),
});
export type DbConfig = z.infer<typeof DbConfigSchema>;

export function ConfigureDb({ db, onSave, dbId }: { dbId: string; db: ApiTypes["DbConfig"]; onSave: () => void }) {
	const update = useSaveDb();
	const {
		register,
		handleSubmit,
		watch,
		setValue,
		formState: { errors },
	} = useForm<DbConfig>({
		resolver: zodResolver(DbConfigSchema),
		defaultValues: {
			...db,
			useSsl: db.useSsl ?? false,
			port: db.port ?? "5432",
		},
	});
	function onSubmit(data: DbConfig) {
		update.mutate({ dbId, data }, { onSuccess: onSave });
	}
	const useSsl = watch("useSsl");

	return (
		<>
			<form onSubmit={handleSubmit(onSubmit)} className="flex flex-col gap-4">
				<Label htmlFor="name">Friendly Name</Label>
				<Input {...register("name")} placeholder="My Database" />
				{errors.name && <p className="text-red-500">{errors.name.message}</p>}

				<div className="grid grid-cols-[1fr_1fr_auto] gap-2">
					<Label htmlFor="host">Host</Label>
					<Label htmlFor="port" className="col-span-2">
						Port
					</Label>

					<Input {...register("host")} placeholder="localhost" />
					<Input placeholder="5432" {...register("port")} defaultValue="5432" />

					<div className="flex items-center gap-2 my-2">
						<Checkbox checked={useSsl ?? false} onCheckedChange={(v) => setValue("useSsl", Boolean(v))} id="useSsl" />
						<Label htmlFor="useSsl">Use SSL</Label>
					</div>

					{(errors.host || errors.port) && (
						<>
							<p className="text-red-500">{errors.host?.message ?? ""}</p>
							<p className="text-red-500">{errors.port?.message ?? ""}</p>
						</>
					)}
				</div>

				<Label htmlFor="host">Database Name</Label>
				<Input {...register("database")} placeholder="postgres" />
				{errors.database && <p className="text-red-500">{errors.database.message}</p>}

				<Label htmlFor="username">Replication User</Label>
				<Input {...register("user")} />
				{errors.user && <p className="text-red-500">{errors.user.message}</p>}

				<Label htmlFor="password">Replication Password</Label>
				<Input type="password" {...register("password")} />
				{errors.password && <p className="text-red-500">{errors.password.message}</p>}

				<span className="text-sm text-muted-foreground">SQL to create the replication user</span>
				<Textarea value={sqlHelpers.createUser(watch("user"), watch("password"))} />
				<Button pending={update.isPending} type="submit">
					Save
				</Button>
			</form>
		</>
	);
}
