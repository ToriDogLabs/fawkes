import { Toaster } from "@/components/ui/toaster";
import { ReactQueryDevtools } from "@tanstack/react-query-devtools";
import { RouterProvider, createRouter } from "@tanstack/react-router";
import { StrictMode } from "react";
import ReactDOM from "react-dom/client";

//theme provider
import { ThemeProvider } from "@/components/ui/themeProvider";

// Import the generated route tree
import { routeTree } from "./routeTree.gen";

//global styles
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import "./index.css";

// Create a new router instance
const router = createRouter({ routeTree });

// Register the router instance for type safety
declare module "@tanstack/react-router" {
	interface Register {
		router: typeof router;
	}
}

const queryClient = new QueryClient();

// Render the app
const rootElement = document.getElementById("root")!;
if (!rootElement.innerHTML) {
	const root = ReactDOM.createRoot(rootElement);
	root.render(
		<StrictMode>
			<ThemeProvider defaultTheme="dark" storageKey="vite-ui-theme">
				<QueryClientProvider client={queryClient}>
					<RouterProvider router={router} />
					<Toaster />
					<ReactQueryDevtools />
				</QueryClientProvider>
			</ThemeProvider>
		</StrictMode>
	);
}
