#!/bin/bash

if [ -e /var/lib/postgresql/data/postgresql.conf ]
then
    echo "Data dir already exists. Skipping recovery"
else
	if [[ -z "${RECOVER_ID}" ]]; then
		echo "Missing RECOVER_ID. Skipping recovery"
	else
		curl --connect-timeout 30 --max-time 300 --retry 10 --retry-delay 5 --retry-connrefused \
			"${RECOVERY_URL}/recoverSteps?config=${RECOVER_ID}" \
			-H 'accept: application/json' | python3 /usr/local/bin/recovery/recover.py
	fi
fi


# Start PostgreSQL
exec /usr/local/bin/docker-entrypoint.sh postgres