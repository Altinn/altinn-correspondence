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

RUN case "${TARGETARCH}" in \
      amd64) RUNNER_ARCH="linux-x64" ;; \
      arm64) RUNNER_ARCH="linux-arm64" ;; \
      arm) RUNNER_ARCH="linux-arm" ;; \
      *) echo "Unsupported TARGETARCH: ${TARGETARCH}" && exit 1 ;; \
    esac \
    && ARCHIVE_NAME="actions-runner-${RUNNER_ARCH}-${RUNNER_VERSION}.tar.gz" \
    && curl -fsSL -o "${ARCHIVE_NAME}" "https://github.com/actions/runner/releases/download/v${RUNNER_VERSION}/${ARCHIVE_NAME}" \
    && tar xzf "${ARCHIVE_NAME}" \
    && rm "${ARCHIVE_NAME}" \
    && ./bin/installdependencies.sh

COPY entrypoint.sh /entrypoint.sh
RUN chmod +x /entrypoint.sh && chown -R runner:runner /home/runner

USER runner
ENTRYPOINT ["/entrypoint.sh"]
