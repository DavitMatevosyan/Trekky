# Nightly pg_dump with off-VM copy (ADR-0004). Same Postgres major version as the database, plus rclone.
FROM postgres:17
RUN apt-get update \
    && apt-get install -y --no-install-recommends rclone ca-certificates \
    && rm -rf /var/lib/apt/lists/*
COPY backup-once.sh schedule.sh /usr/local/bin/
RUN chmod +x /usr/local/bin/backup-once.sh /usr/local/bin/schedule.sh
ENTRYPOINT ["/usr/local/bin/schedule.sh"]
