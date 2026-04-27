FROM ubuntu:24.04

ARG RUNNER_VERSION=2.326.0
ARG TARGETARCH=amd64

ENV DEBIAN_FRONTEND=noninteractive
ENV RUNNER_ALLOW_RUNASROOT=1

RUN apt-get update \
    && apt-get install -y --no-install-recommends \
        ca-certificates \
        curl \
        git \
        jq \
        tar \
        unzip \
        wget \
    && rm -rf /var/lib/apt/lists/*

RUN useradd -m -d /home/runner -s /bin/bash runner
WORKDIR /home/runner

RUN ARCHIVE_NAME="actions-runner-linux-${TARGETARCH}-${RUNNER_VERSION}.tar.gz" \
    && curl -fsSL -o "${ARCHIVE_NAME}" "https://github.com/actions/runner/releases/download/v${RUNNER_VERSION}/${ARCHIVE_NAME}" \
    && tar xzf "${ARCHIVE_NAME}" \
    && rm "${ARCHIVE_NAME}" \
    && ./bin/installdependencies.sh

COPY entrypoint.sh /entrypoint.sh
RUN chmod +x /entrypoint.sh && chown -R runner:runner /home/runner

USER runner
ENTRYPOINT ["/entrypoint.sh"]
