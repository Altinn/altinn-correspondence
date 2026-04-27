FROM ubuntu:24.04

ARG RUNNER_VERSION=2.326.0
ARG TARGETARCH=amd64

ENV DEBIAN_FRONTEND=noninteractive
ENV RUNNER_ALLOW_RUNASROOT=1

RUN apt-get update \
    && apt-get install -y --no-install-recommends \
        apt-transport-https \
        ca-certificates \
        curl \
        git \
        gnupg \
        jq \
        lsb-release \
        docker.io \
        tar \
        unzip \
        wget \
    && mkdir -p /etc/apt/keyrings \
    && curl -sL https://packages.microsoft.com/keys/microsoft.asc | gpg --dearmor | tee /etc/apt/keyrings/microsoft.gpg > /dev/null \
    && SUITE=$(lsb_release -cs) \
    && echo "deb [arch=amd64 signed-by=/etc/apt/keyrings/microsoft.gpg] https://packages.microsoft.com/repos/azure-cli/ $SUITE main" > /etc/apt/sources.list.d/azure-cli.list \
    && apt-get update \
    && apt-get install -y --no-install-recommends azure-cli \
    && az aks install-cli --install-location /usr/local/bin/kubectl --kubelogin-install-location /usr/local/bin/kubelogin \
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

ENTRYPOINT ["/entrypoint.sh"]
