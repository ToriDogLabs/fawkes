import { getServerBackupidRecoverCmd } from "@/hooks/useApi";
import { useQuery } from "@tanstack/react-query";
import { useCopyToClipboard } from "@uidotdev/usehooks";
import { CopyIcon } from "lucide-react";
import { useCallback, useState } from "react";
import { Button } from "./ui/button";
import { Input } from "./ui/input";
import { Label } from "./ui/label";
import { RadioGroup, RadioGroupItem } from "./ui/radio-group";
import { ScrollArea } from "./ui/scroll-area";
import { toast } from "./ui/use-toast";

function useRecoveryScript({ dbId, backupId, s3Id }: { dbId: string; backupId: string; s3Id: string }) {
	const [targetTime, setTargetTime] = useState("");
	const query = useQuery(getServerBackupidRecoverCmd({ dbId, backupId, targetTime, s3Id }));

	const update = useCallback(
		(targetTime: string) => {
			setTargetTime(targetTime);
		},
		[setTargetTime]
	);
	return [query, update] as const;
}

function RecoverScript({ script }: { script: string }) {
	const [dir, setDir] = useState("/var/lib/postgresql/data");
	const [_copiedText, copyToClipboard] = useCopyToClipboard();

	const cmd = script + ` ${dir}`;

	return (
		<>
			<Label htmlFor="restoreDir">Restore Directory</Label>
			<Input id="restoreDir" value={dir} onChange={(e) => setDir(e.target.value)} />
			<div className="bg-slate-900 p-4 rounded-md flex gap-2 items-center">
				<ScrollArea viewportClassName="max-h-56">
					<div className="break-all">{cmd}</div>
				</ScrollArea>
				<Button
					variant="ghost"
					onClick={() => {
						copyToClipboard(cmd);
						toast({
							title: "Command copied",
							variant: "success",
							duration: 2000,
						});
					}}
				>
					<CopyIcon />
				</Button>
			</div>
		</>
	);
}

export function RecoverContent({ backupId, dbId, s3Id }: { backupId: string; dbId: string; s3Id: string }) {
	const [targetTime, setTargetTime] = useState<string>("");
	const [timeType, setTimeType] = useState<"latest" | "time">("latest");
	const [scriptQuery, update] = useRecoveryScript({ dbId, backupId, s3Id });

	return (
		<>
			<Label>Time To Restore</Label>
			<RadioGroup
				value={timeType}
				onValueChange={(e) => {
					setTimeType(e as "latest" | "time");
					update(e === "latest" ? "" : targetTime);
				}}
			>
				<div className="flex gap-2 items-center h-10">
					<RadioGroupItem value="latest" id="latest" />
					<Label htmlFor="latest">Latest</Label>
				</div>
				<div className="flex gap-2 items-center h-10">
					<RadioGroupItem value="time" id="time" />
					<Label htmlFor="time">Time</Label>
					{timeType === "time" && (
						<Input
							type="datetime-local"
							value={targetTime}
							onChange={(e) => {
								setTargetTime(e.target.value);
								update(e.target.value);
							}}
							className="w-56"
						/>
					)}
				</div>
			</RadioGroup>

			<RecoverScript script={scriptQuery.data?.script ?? ""} />
		</>
	);
}
