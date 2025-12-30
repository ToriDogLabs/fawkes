import { useToast } from "@/components/ui/use-toast";
import { ApiTypes } from "@/gen/api";
import { getToastVariant, useServerMessages } from "@/stores/serverMessages";
import * as signalR from "@microsoft/signalr";
import { useQuery } from "@tanstack/react-query";
import { useEffect } from "react";
import { useClientMethod } from "react-use-signalr";
import { queries, useAddBackupGroup, useRefreshBackups, useRemoveBackup } from "./useApi";

const baseUrl = location.port === "3001" ? "http://localhost:62082" : "";

const signalrConnection = new signalR.HubConnectionBuilder().withUrl(`${baseUrl}/api/signalr`).withAutomaticReconnect().build();
signalrConnection.start();

function useSignalr(methodName: string, callback: (...args: any[]) => void) {
	useClientMethod(signalrConnection, methodName, callback);
}

interface ServerMessage {
	action: "serverMessage";
	serverId: string;
	notification: ApiTypes["Notification"];
	id: string;
}

interface ClearServerMessage {
	action: "clearServerMessage";
	serverId: string;
	message: string | null;
	variant?: ApiTypes["Notification"]["variant"];
	id: string;
}

interface RefreshBackups {
	action: "refreshBackups";
	server: string;
}

interface AddBackupGroup {
	action: "addBackupGroup";
	group: ApiTypes["BackupGroup"];
	dbId: string;
}

interface RemoveBackup {
	action: "removeBackup";
	dbId: string;
	backupId: string;
	locationId: string;
}

type Message = ServerMessage | ClearServerMessage | RefreshBackups | AddBackupGroup | RemoveBackup;

export function SignalrStateSync() {
	const updateServerMessage = useServerMessages.use.updateServerMessage();
	const removeServerMessage = useServerMessages.use.removeServerMessage();
	const sync = useServerMessages.use.sync();
	const { toast } = useToast();
	const refreshBackups = useRefreshBackups();
	const addBackupGroup = useAddBackupGroup();
	const removeBackup = useRemoveBackup();
	const { data: notifications } = useQuery({
		...queries.getNotifications(),
	});
	useEffect(() => {
		if (notifications !== undefined) {
			sync(notifications);
		}
	}, [notifications]);
	useSignalr("sync", (data: Message) => {
		if (data.action === "serverMessage") {
			updateServerMessage(data.notification);
			if (data.notification.variant !== "OnGoing") {
				toast({
					title: data.notification.message,
					variant: getToastVariant(data.notification.variant),
				});
			}
		} else if (data.action === "clearServerMessage") {
			removeServerMessage(data.id);
			if (data.message) {
				toast({
					title: data.message,
					variant: getToastVariant(data.variant),
				});
			}
		} else if (data.action === "refreshBackups") {
			refreshBackups(data.server);
		} else if (data.action === "addBackupGroup") {
			addBackupGroup(data.group, data.dbId);
		} else if (data.action === "removeBackup") {
			removeBackup(data.dbId, data.backupId, data.locationId);
		}
	});
	return null;
}
