import { createRootRoute, Link, Outlet } from "@tanstack/react-router";
import { TanStackRouterDevtools } from "@tanstack/router-devtools";

// components
import { MessageAlerts } from "@/components/MessageAlerts";
import { Button } from "@/components/ui/button";
import { ModeToggle } from "@/components/ui/modeToggle";
import { TooltipProvider } from "@/components/ui/tooltip";
import { SignalrStateSync } from "@/hooks/useSignalr";
import { HomeIcon } from "lucide-react";

function PageContent() {
	return (
		<>
			<SignalrStateSync />
			<main className="flex min-h-[calc(100vh_-_theme(spacing.16))] flex-1 flex-col gap-4 bg-muted/40 p-4 md:gap-8 md:p-10">
				<div className="mx-auto grid w-full max-w-6xl gap-2">
					<h1 className="text-3xl font-semibold"></h1>
				</div>
				<div className="mx-auto w-full max-w-6xl items-start gap-6">
					<Outlet />
				</div>
			</main>
		</>
	);
}

function Navbar() {
	return (
		<header className="px-2 flex gap-2 items-center h-14 sticky top-0 bg-background border-b z-50">
			<Link to="/" className="flex items-center gap-2">
				<Button variant="outline" size="icon">
					<HomeIcon />
				</Button>
			</Link>
			<MessageAlerts />
			<div className="flex-1"></div>
			<ModeToggle />
		</header>
	);
}

function PageStructure() {
	return (
		<div className="flex flex-col min-h-[100vh]">
			<Navbar />
			<div className="flex-1 flex flex-col">
				<PageContent />
			</div>
			{false && <TanStackRouterDevtools />}
		</div>
	);
}

export const Route = createRootRoute({
	component: () => (
		<TooltipProvider>
			<PageStructure />
		</TooltipProvider>
	),
});
