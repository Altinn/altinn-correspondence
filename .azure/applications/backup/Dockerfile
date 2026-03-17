FROM ubuntu:22.04

RUN apt-get update \
    && apt-get install -y ca-certificates curl gnupg lsb-release apt-transport-https \
    && mkdir -p /etc/apt/keyrings \
    && curl -sL https://packages.microsoft.com/keys/microsoft.asc | gpg --dearmor | tee /etc/apt/keyrings/microsoft.gpg > /dev/null \
    && curl -sL https://www.postgresql.org/media/keys/ACCC4CF8.asc | gpg --dearmor | tee /etc/apt/keyrings/postgresql.gpg > /dev/null \
    && SUITE=$(lsb_release -cs) \
    && echo "deb [arch=amd64 signed-by=/etc/apt/keyrings/microsoft.gpg] https://packages.microsoft.com/repos/azure-cli/ $SUITE main" > /etc/apt/sources.list.d/azure-cli.list \
    && echo "deb [signed-by=/etc/apt/keyrings/postgresql.gpg] http://apt.postgresql.org/pub/repos/apt/ $SUITE-pgdg main" > /etc/apt/sources.list.d/pgdg.list \
    && apt-get update \
    && apt-get install -y azure-cli postgresql-client-16 \
    && apt-get clean \
    && rm -rf /var/lib/apt/lists/*
