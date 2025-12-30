import type { ApiTypes } from "./gen/api";

const monthlyArchivePolicy: ApiTypes["ArchivalPolicy"] = {
	strategy: {
		dayOfWeek: "Sunday",
		occurrence: 1,
		occurrenceUnits: "Month",
		type: "TargetDay",
	},
	retention: {
		duration: 1,
		units: "Year",
	},
};

const yearlyArchivePolicy: ApiTypes["ArchivalPolicy"] = {
	strategy: {
		dayOfWeek: "Sunday",
		occurrence: 1,
		occurrenceUnits: "Year",
		type: "TargetDay",
	},
	retention: {
		duration: 5,
		units: "Year",
	},
};

export const DEFAULT_ARCHIVAL_POLICIES = {
	monthly: monthlyArchivePolicy,
	yearly: yearlyArchivePolicy,
};
