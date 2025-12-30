import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { ApiTypes } from "@/gen/api";
import { useSaveS3 } from "@/hooks/useApi";
import { useForm } from "react-hook-form";
import { Checkbox } from "./ui/checkbox";
import { DialogFooter } from "./ui/dialog";
import { useToast } from "./ui/use-toast";

export function ConfigureBackupLocation({
	s3Config,
	id,
	onSave,
}: {
	s3Config?: ApiTypes["S3BackupLocation"];
	id: string;
	onSave: () => void;
}) {
	const update = useSaveS3();

	const { toast } = useToast();
	const { register, handleSubmit, watch, setValue } = useForm<ApiTypes["S3BackupLocation"]>({
		defaultValues: s3Config ?? {},
	});

	const forcePathStyle = watch("forcePathStyle");

	function onSubmit(data: ApiTypes["S3BackupLocation"]) {
		update.mutate(
			{ id, data },
			{
				onError(error: any) {
					toast({
						title: error?.title ?? `Error connecting to s3 server.`,
						variant: "destructive",
					});
				},
				onSuccess() {
					toast({
						title: "S3 backend saved.",
						variant: "success",
					});
					onSave();
				},
			}
		);
	}

	return (
		<form onSubmit={handleSubmit(onSubmit)} className="flex flex-col gap-2">
			<Label htmlFor="friendlyName">Friendly Name</Label>
			<Input id="friendlyName" {...register("friendlyName")} required />

			<Label htmlFor="origin">Endpoint Url</Label>
			<Input id="origin" {...register("endpoint")} required />

			<Label htmlFor="bucket">Bucket</Label>
			<Input id="bucket" {...register("bucket")} required />

			<Label htmlFor="prefix">Path</Label>
			<Input id="prefix" {...register("prefix")} />

			<div className="flex items-center gap-2 my-2">
				<Checkbox
					checked={forcePathStyle ?? false}
					onCheckedChange={(v) => setValue("forcePathStyle", Boolean(v))}
					id="forcePathStyle"
				/>
				<Label htmlFor="forcePathStyle">Force Path Style</Label>
			</div>

			<Label htmlFor="accessKey">Access Key</Label>
			<Input id="accessKey" {...register("accessKey")} required />

			<Label htmlFor="secretKey">Secret Key</Label>
			<Input type="password" id="secretKey" {...register("secretKey")} required />
			<DialogFooter>
				<Button pending={update.isPending} type="submit">
					Save
				</Button>
			</DialogFooter>
		</form>
	);
}
