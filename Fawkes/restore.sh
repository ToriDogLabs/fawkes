#!/bin/sh

if [ -e ${PGDATA}/postgresql.conf ]
then
	echo "Postgres directory already exists, unable to restore."
else
	short=a:k:e:s:p:i:t:
	long=access_key:,secret_key:,endpoint:,server:,path:,id:,target_time:

	TEMP=$(getopt -o $short --long $long -- ${ARGS})

	eval set -- "${TEMP}"

	while :; do
			case "${1}" in
					-k | --secret_key	) secret_key=$2;        shift 2 ;;
					-a | --access_key   ) access_key=$2;        shift 2 ;;
					-e | --endpoint		) endpoint=$2;			shift 2 ;;
					-s | --server		) server=$2;			shift 2 ;;
					-p | --path			) path=$2;				shift 2 ;;
					-i | --id			) id=$2;				shift 2 ;;
					-t | --target_time	) target_time=$2;       shift 2 ;;
					--					) shift;                break ;;
					*					) echo "Error parsing"; exit 1 ;;
			esac
	done

	echo "***************STARTING RECOVERY SCRIPT***************"

	mkdir -p ~/.aws
	echo "[default]" > ~/.aws/credentials
	echo "aws_access_key_id=${access_key}" >> ~/.aws/credentials
	echo "aws_secret_access_key=${secret_key}" >> ~/.aws/credentials
	echo "Restoring base backup..."
	
	barman-cloud-restore --endpoint-url ${endpoint} ${path} ${server} ${id} ${PGDATA}
	echo "Finished"
	
	rm ~/.aws/credentials
	cp ${PGDATA}/postgresql.conf ${PGDATA}/postgresql.conf.backup
	echo "restore_command = 'barman-cloud-wal-restore --endpoint-url ${endpoint} ${path} ${server} %f %p'" >> ${PGDATA}/postgresql.conf
	if [ ! -z "${target_time}" ];
	then
		echo "recovery_target_time = '${target_time}'" >> ${PGDATA}/postgresql.conf
	fi
	echo "" > ${PGDATA}/recovery.signal

	mkdir -p /var/lib/postgresql/.aws
	echo "[default]" > /var/lib/postgresql/.aws/credentials
	echo "aws_access_key_id=${access_key}" >> /var/lib/postgresql/.aws/credentials
	echo "aws_secret_access_key=${secret_key}" >> /var/lib/postgresql/.aws/credentials

	echo "Starting Postgres"
	/usr/local/bin/docker-entrypoint.sh postgres &

	wait=1
	while [ $wait -ne 0 ]
	do
		sleep 1s
		pg_isready > /dev/null
		wait=$?
	done
	sleep 1s
	echo "Stopping postgres"
	su -c "pg_ctl stop" postgres
	echo "Postgres stopped"

	echo "Cleaning up"
	rm /var/lib/postgresql/.aws/credentials
	rm ${PGDATA}/postgresql.conf
	rm ${PGDATA}/recovery.signal
	mv ${PGDATA}/postgresql.conf.backup ${PGDATA}/postgresql.conf
	
	echo "Finished restoring"
fi