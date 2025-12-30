import { DEFAULT_ARCHIVAL_POLICIES } from "@/ArchivalPolicy";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { RadioGroup, RadioGroupItem } from "@/components/ui/radio-group";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import type { ApiTypes } from "@/gen/api";
import { queries, useDeleteArchivePolicy, usePutArchivePolicy } from "@/hooks/useApi";
import { makeUuid } from "@/utils/make-uuid";
import { useQuery } from "@tanstack/react-query";
import { SettingsIcon, Trash2Icon } from "lucide-react";
import { useState } from "react";
import { useForm } from "react-hook-form";
import { Button } from "./ui/button";
import { Card, CardContent, CardDescription, CardFooter, CardHeader, CardTitle } from "./ui/card";

function DayOfWeekPicker({
	value = "Sunday",
	onChange,
	onFocus,
}: {
	value: ApiTypes["IsoDayOfWeek"] | undefined;
	onChange(value: ApiTypes["IsoDayOfWeek"]): void;
	onFocus?: React.FocusEventHandler<HTMLDivElement>;
}) {
	return (
		<Select value={value?.toString() ?? ""} onValueChange={(e) => onChange(e as any)}>
			<SelectTrigger className="w-32">
				<SelectValue />
			</SelectTrigger>
			<SelectContent side="top" onFocus={onFocus}>
				{["Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday"].map((dayOfWeek) => (
					<SelectItem key={dayOfWeek} value={dayOfWeek}>
						{dayOfWeek}
					</SelectItem>
				))}
			</SelectContent>
		</Select>
	);
}

function OccurrencePicker({
	value = 1,
	onChange,
	max,
	onFocus,
}: {
	value: number | undefined;
	onChange(value: number): void;
	max: number;
	onFocus?: React.FocusEventHandler<HTMLDivElement>;
}) {
	return (
		<Select value={value?.toString() ?? ""} onValueChange={(e) => onChange(parseInt(e))}>
			<SelectTrigger className="w-20">
				<SelectValue />
			</SelectTrigger>
			<SelectContent side="top" onFocus={onFocus}>
				{Array.from({ length: max }, (_, i) => (
					<SelectItem key={i} value={`${i + 1}`}>
						{i + 1}
						{getOrdinal(i + 1)}
					</SelectItem>
				))}
			</SelectContent>
		</Select>
	);
}

function getOrdinal(n: number | undefined) {
	if (n === undefined) return "";
	const suffixes = ["th", "st", "nd", "rd"];
	const v = n % 100;
	return suffixes[(v - 20) % 10] || suffixes[v] || suffixes[0];
}

function UnitsPicker({
	value = "month",
	onChange,
	limitTo = ["Day", "Week", "Month", "Year"],
	plural = false,
	onFocus,
}: {
	value: string | undefined;
	onChange: (value: string) => void;
	limitTo?: ApiTypes["DurationUnits"][];
	plural?: boolean;
	onFocus?: React.FocusEventHandler<HTMLDivElement>;
}) {
	const suffix = plural ? "s" : "";
	return (
		<Select value={value} onValueChange={(e) => onChange(e)}>
			<SelectTrigger className="w-32">
				<SelectValue />
			</SelectTrigger>
			<SelectContent side="top" onFocus={onFocus}>
				{limitTo.includes("Day") && <SelectItem value="Day">Day{suffix}</SelectItem>}
				{limitTo.includes("Week") && <SelectItem value="Week">Week{suffix}</SelectItem>}
				{limitTo.includes("Month") && <SelectItem value="Month">Month{suffix}</SelectItem>}
				{limitTo.includes("Year") && <SelectItem value="Year">Year{suffix}</SelectItem>}
			</SelectContent>
		</Select>
	);
}

function humanize(policy: ApiTypes["ArchivalPolicy"]) {
	console.log(policy);
	let text = "";
	if (policy.strategy.type === "DayOfWeek") {
		text += "Backups taken on " + policy.strategy.dayOfWeek;
	} else if (policy.strategy.type === "TargetDay") {
		text += `Backups taken on the ${policy.strategy.occurrence}${getOrdinal(policy.strategy.occurrence)} ${
			policy.strategy.dayOfWeek
		} of the ${policy.strategy.occurrenceUnits}`;
	}

	const suffix = (policy.retention.duration ?? 0) > 1 ? "s" : "";
	text += ` are archived for ${policy.retention.duration}`;
	if (policy.retention.units === "Day") {
		text += ` day${suffix}.`;
	} else if (policy.retention.units === "Week") {
		text += ` week${suffix}.`;
	} else if (policy.retention.units === "Month") {
		text += ` month${suffix}.`;
	} else if (policy.retention.units === "Year") {
		text += ` year${suffix}.`;
	}
	return text;
}

function createDefaultArchivalPolicy(): ApiTypes["ArchivalPolicy"] {
	return {
		id: makeUuid(),
		retention: {
			...DEFAULT_ARCHIVAL_POLICIES.monthly.retention,
		},
		strategy: {
			dayOfWeek: "Sunday",
			...DEFAULT_ARCHIVAL_POLICIES.monthly.strategy,
		},
	};
}

export function ArchivalEditor({ policy, dbId, onComplete }: { policy: ApiTypes["ArchivalPolicy"]; dbId: string; onComplete: () => void }) {
	const { register, handleSubmit, watch, setValue, getValues } = useForm<ApiTypes["ArchivalPolicy"]>({
		defaultValues: policy,
	});
	const retentionUnits = watch("retention.units");
	const strategyType = watch("strategy.type");
	const dayOfWeek = watch("strategy.dayOfWeek");
	const { mutate: updatePolicy, isPending } = usePutArchivePolicy();

	function setStrategyType(type: ApiTypes["ArchivalPolicy"]["strategy"]["type"]) {
		setValue("strategy.type", type);
	}
	return (
		<form
			onSubmit={handleSubmit((policy) => {
				updatePolicy(
					{ dbId: dbId, policy },
					{
						onSuccess() {
							onComplete();
						},
					}
				);
			})}
		>
			<>
				<CardContent className="flex flex-col gap-2 items-start">
					<RadioGroup className="w-full" value={strategyType} onValueChange={(e) => setStrategyType(e as any)}>
						<div className="flex gap-2 items-center h-10">
							<RadioGroupItem value="DayOfWeek" id="dayOfWeek" />
							<div className="flex gap-2 items-center w-full">
								<Label htmlFor="dayOfWeek" className="cursor-pointer">
									Backup taken on
								</Label>
								<DayOfWeekPicker
									value={dayOfWeek}
									onChange={(e) => setValue("strategy.dayOfWeek", e)}
									onFocus={() => setStrategyType("DayOfWeek")}
								/>
							</div>
						</div>
						<div className="flex gap-2 items-center h-10">
							<RadioGroupItem value="TargetDay" id="targetDay" />
							<div className="flex gap-2 items-center w-full">
								<Label htmlFor="targetDay" className="cursor-pointer">
									Backup taken on the
								</Label>
								<OccurrencePicker
									value={watch("strategy.occurrence")}
									onChange={(e) => setValue("strategy.occurrence", e)}
									max={watch("strategy.occurrenceUnits") === "Month" ? 5 : 53}
									onFocus={() => setStrategyType("TargetDay")}
								/>
								<DayOfWeekPicker
									value={dayOfWeek}
									onChange={(e) => setValue("strategy.dayOfWeek", e)}
									onFocus={() => setStrategyType("TargetDay")}
								/>
								<Label htmlFor="targetDay" className="cursor-pointer">
									of the
								</Label>
								<UnitsPicker
									value={watch("strategy.occurrenceUnits")}
									onChange={(e) => setValue("strategy.occurrenceUnits", e as any)}
									limitTo={["Month", "Year"]}
									onFocus={() => setStrategyType("TargetDay")}
								/>
							</div>
						</div>
					</RadioGroup>
					<Label className="text-pretty text-lg">Archive for </Label>
					<div className="flex gap-2 items-center">
						<Input {...register("retention.duration")} type="number" className="w-20" />
						<UnitsPicker value={retentionUnits} onChange={(e) => setValue("retention.units", e as any)} plural />
					</div>
					<div className="text-pretty text-lg mt-8">
						<p className="font-bold text-muted-foreground">Summary</p>
						<p>{humanize(getValues())}</p>
					</div>
				</CardContent>
				<CardFooter className="flex gap-2">
					<Button type="submit" pending={isPending}>
						Save
					</Button>
					<Button variant="outline" type="button" onClick={() => onComplete()}>
						Cancel
					</Button>
				</CardFooter>
			</>
		</form>
	);
}

export function ArchivalCard({ dbId }: { dbId: string }) {
	const policies = useQuery(queries.getArchivePolicies(dbId));
	const [editingPolicy, setEditingPolicy] = useState<ApiTypes["ArchivalPolicy"] | null>(null);
	const deletePolicy = useDeleteArchivePolicy();

	return (
		<Card>
			<CardHeader>
				<CardTitle className="text-pretty text-2xl">Long Term Archival Policies</CardTitle>
				<CardDescription>If a backup matches any of these policies it will not be deleted.</CardDescription>
			</CardHeader>
			{editingPolicy && (
				<>
					<ArchivalEditor policy={editingPolicy} dbId={dbId} onComplete={() => setEditingPolicy(null)} />
				</>
			)}
			{editingPolicy === null && (
				<>
					<CardContent>
						<ul className="list-disc pl-6">
							{policies.data?.map((policy) => {
								const policyId = policy.id;
								if (!policyId) return null;
								return (
									<li key={policy.id}>
										<div className="flex gap-2 items-center">
											{humanize(policy)}
											<Button variant="ghost" size="icon" onClick={() => setEditingPolicy(policy)}>
												<SettingsIcon className="size-5" />
											</Button>
											<Button
												variant="ghost"
												size="icon"
												onClick={() => {
													deletePolicy.mutate({ dbId, policyId });
												}}
												pending={deletePolicy.isPending}
											>
												<Trash2Icon className="size-5" />
											</Button>
										</div>
									</li>
								);
							})}
						</ul>
						{policies.data?.length === 0 && <div className="text-center text-sm text-muted-foreground">No policies</div>}
					</CardContent>

					<CardFooter>
						<Button onClick={() => setEditingPolicy(createDefaultArchivalPolicy())}>Add Policy</Button>
					</CardFooter>
				</>
			)}
		</Card>
	);
}
