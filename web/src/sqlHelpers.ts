import type { ApiTypes } from "./gen/api";

export const sqlHelpers = {
	createUser: (user: string, password: string) => `CREATE USER ${user} PASSWORD '${password}' replication;
GRANT EXECUTE ON FUNCTION pg_backup_start(text, boolean) to ${user};
GRANT EXECUTE ON FUNCTION pg_backup_stop(boolean) to ${user};
GRANT pg_checkpoint TO ${user};
GRANT EXECUTE ON FUNCTION pg_switch_wal() to ${user};
GRANT EXECUTE ON FUNCTION pg_create_restore_point(text) to ${user};
GRANT pg_read_all_settings TO ${user};
GRANT pg_read_all_stats TO ${user};`,
	hba: (user: string) => `host replication ${user} 0.0.0.0/0 scram-sha-256`,
	walActivity: "CREATE TABLE ____tmp_barman_wal(id INT); INSERT INTO ____tmp_barman_wal VALUES (1); DROP TABLE ____tmp_barman_wal;",
	cleanup: (
		db: ApiTypes["DatabaseSettings"]
	) => `select pg_terminate_backend(select active_pid from pg_replication_slots where slot_name = fawkes_${db.id.replace(
		/-/g,
		"_"
	)}) from pg_stat_replication;
DROP OWNED BY ${db.replicationUser}; DROP USER ${db.replicationUser};
select pg_drop_replication_slot('fawkes_${db.id.replace(/-/g, "_")}');`,
};
