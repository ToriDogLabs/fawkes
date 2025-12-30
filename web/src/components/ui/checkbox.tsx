import * as CheckboxPrimitive from "@radix-ui/react-checkbox";
import { Check } from "lucide-react";
import * as React from "react";

import { cn } from "@/utils/css";
import { Label } from "./label";

const Checkbox = React.forwardRef<
	React.ElementRef<typeof CheckboxPrimitive.Root>,
	React.ComponentPropsWithoutRef<typeof CheckboxPrimitive.Root>
>(({ className, ...props }, ref) => (
	<CheckboxPrimitive.Root
		ref={ref}
		className={cn(
			"peer h-4 w-4 shrink-0 rounded-sm border border-primary ring-offset-background focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 disabled:cursor-not-allowed disabled:opacity-50 data-[state=checked]:bg-primary data-[state=checked]:text-primary-foreground",
			className
		)}
		{...props}
	>
		<CheckboxPrimitive.Indicator className={cn("flex items-center justify-center text-current")}>
			<Check className="h-4 w-4" />
		</CheckboxPrimitive.Indicator>
	</CheckboxPrimitive.Root>
));
Checkbox.displayName = CheckboxPrimitive.Root.displayName;

const SimpleCheckbox = React.forwardRef<
	React.ElementRef<typeof CheckboxPrimitive.Root>,
	React.ComponentPropsWithoutRef<typeof CheckboxPrimitive.Root> & {
		label: string;
	}
>(({ id: propsId, label, ...props }, ref) => {
	const generatedId = React.useId();
	const id = propsId ?? generatedId;
	return (
		<div className="flex items-center gap-1">
			<Checkbox ref={ref} {...props} id={id} />
			<Label htmlFor={id} className="cursor-pointer">
				{label}
			</Label>
		</div>
	);
});
SimpleCheckbox.displayName = "SimpleCheckbox";

export { Checkbox, SimpleCheckbox };
