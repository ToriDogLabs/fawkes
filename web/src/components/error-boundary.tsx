import type { ReactNode } from "react";
import { Component } from "react";

export class ErrorBoundary extends Component<{ children: ReactNode; fallback: ReactNode }, { hasError: boolean }> {
	constructor(props: any) {
		super(props);
		this.state = { hasError: false };
	}

	static getDerivedStateFromError(error: any) {
		console.error(error);
		// Update state so the next render will show the fallback UI.
		return { hasError: true };
	}

	componentDidCatch(error: any, info: any) {
		console.error(
			error,
			// Example "componentStack":
			//   in ComponentThatThrows (created by App)
			//   in ErrorBoundary (created by App)
			//   in div (created by App)
			//   in App
			info.componentStack
		);
	}

	render() {
		if (this.state.hasError) {
			// You can render any custom fallback UI
			return this.props.fallback;
		}

		return this.props.children;
	}
}
