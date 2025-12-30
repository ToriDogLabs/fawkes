import { BackupsPage } from "@/components/BackupsPage";
import { createFileRoute } from "@tanstack/react-router";
import { z } from "zod";

export const Route = createFileRoute("/db")({
	component: Page,
	validateSearch: z.object({
		dbId: z.string(),
	}),
});

function Page() {
	const { dbId } = Route.useSearch();
	return <BackupsPage dbId={dbId} />;
}
