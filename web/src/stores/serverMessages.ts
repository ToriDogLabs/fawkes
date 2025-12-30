import { ApiTypes } from "@/gen/api";
import { create, StoreApi, UseBoundStore } from "zustand";
import { mutative } from "zustand-mutative";
import { devtools } from "zustand/middleware";

type WithSelectors<S> = S extends { getState: () => infer T } ? S & { use: { [K in keyof T]: () => T[K] } } : never;

const createSelectors = <S extends UseBoundStore<StoreApi<object>>>(_store: S) => {
	let store = _store as WithSelectors<typeof _store>;
	store.use = {};
	for (let k of Object.keys(store.getState())) {
		(store.use as any)[k] = () => store((s) => s[k as keyof typeof s]);
	}

	return store;
};

type TimestampedNotification = ApiTypes["Notification"] & {
	timestamp: number;
};

interface State {
	notifications: TimestampedNotification[];
	updateServerMessage(notification: ApiTypes["Notification"]): void;
	removeServerMessage(id: string): void;
	sync(notifications: ApiTypes["Notification"][]): void;
}

const useServerMessagesBase = create<State>()(
	devtools(
		mutative((set) => ({
			notifications: [],
			updateServerMessage: (notification) => {
				set((state) => {
					const msg = state.notifications.find((msg) => msg.id === notification.id);
					if (msg) {
						msg.message = notification.message;
						msg.timestamp = Date.now();
					} else {
						state.notifications.push({
							...notification,
							timestamp: Date.now(),
						});
					}
				});
			},
			removeServerMessage: (id) => {
				set((state) => {
					state.notifications = state.notifications.filter((msg) => msg.id !== id);
				});
			},
			sync: (notifications) => {
				set((state) => {
					state.notifications = notifications.map((notification) => ({
						...notification,
						timestamp: Date.now(),
					}));
				});
			},
		}))
	)
);

export const useServerMessages = createSelectors(useServerMessagesBase);

export function useDbNotifications(dbId?: string) {
	return useServerMessages((state) => state.notifications.filter((notification) => dbId === undefined || notification.dbId === dbId));
}

export function getToastVariant(variant: ApiTypes["Notification"]["variant"] | undefined) {
	switch (variant) {
		case "Destructive":
			return "destructive";
		case "Success":
			return "success";
		default:
			return "default";
	}
}
