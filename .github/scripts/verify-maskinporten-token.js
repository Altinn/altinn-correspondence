#!/usr/bin/env node
"use strict";

const crypto = require("crypto");
const { execFileSync } = require("child_process");
const fs = require("fs");
const path = require("path");

const CLIENT_ID_SECRET_NAME = "maskinporten-client-id";
const JWK_SECRET_NAME = "maskinporten-jwk";
const TOKEN_LIFETIME_SECONDS = 120;

function main() {
  const vaultName = requiredEnv("KEY_VAULT_NAME");
  const deployEnvironment = requiredEnv("DEPLOY_ENVIRONMENT");
  const workspace = process.env.GITHUB_WORKSPACE || process.cwd();
  const settings = loadMaskinportenSettings(workspace, deployEnvironment);

  const clientId = readKeyVaultSecret(vaultName, CLIENT_ID_SECRET_NAME);
  const encodedJwk = readKeyVaultSecret(vaultName, JWK_SECRET_NAME);
  mask(encodedJwk);

  const jwk = decodePrivateJwk(encodedJwk);
  const assertion = createClientAssertion({
    jwk,
    clientId,
    scope: settings.scope,
    audience: settings.audience,
  });
  mask(assertion);

  console.log(
    `Requesting Maskinporten token for '${deployEnvironment}' ` +
      `(Maskinporten environment '${settings.maskinportenEnvironment}') at ${settings.tokenEndpoint}.`
  );
  console.log(`Client id: ${clientId}`);
  console.log(`Scopes: ${settings.scope}`);

  return requestToken(settings.tokenEndpoint, assertion).then((result) => {
    if (result.accessToken) {
      mask(result.accessToken);
    }

    if (!result.ok || !result.accessToken) {
      const error = result.error || "<none>";
      const description = result.errorDescription || "<none>";
      throw new Error(
        `Maskinporten token request failed with HTTP ${result.status}. ` +
          `Error=${error}. Description=${description}`
      );
    }

    console.log("Maskinporten accepted the configured scopes.");
  });
}

function loadMaskinportenSettings(workspace, deployEnvironment) {
  const appsettingsDirectory = path.join(workspace, "src", "Altinn.Correspondence.API");
  const baseSettings = readAppsettings(path.join(appsettingsDirectory, "appsettings.json")).MaskinportenSettings || {};
  const environmentFile = path.join(appsettingsDirectory, `appsettings.${deployEnvironment}.json`);
  if (!fs.existsSync(environmentFile)) {
    throw new Error(`No appsettings file for environment '${deployEnvironment}': ${environmentFile}`);
  }

  const environmentSettings = readAppsettings(environmentFile).MaskinportenSettings || {};
  const scope = (baseSettings.Scope || "").trim();
  const maskinportenEnvironment = pickSetting(environmentSettings, baseSettings, "Environment").trim();
  if (!scope) {
    throw new Error("MaskinportenSettings:Scope is empty in appsettings.json.");
  }

  if (!maskinportenEnvironment) {
    throw new Error("MaskinportenSettings:Environment is empty.");
  }

  const production = isProduction(maskinportenEnvironment);
  return {
    scope,
    maskinportenEnvironment,
    tokenEndpoint: production ? "https://maskinporten.no/token" : "https://test.maskinporten.no/token",
    audience: production ? "https://maskinporten.no/" : "https://test.maskinporten.no/",
  };
}

function createClientAssertion({ jwk, clientId, scope, audience }) {
  const issuedAt = Math.floor(Date.now() / 1000);
  const header = base64UrlJson({ alg: "RS256", typ: "JWT", kid: jwk.kid });
  const payload = base64UrlJson({
    aud: audience,
    scope,
    iss: clientId,
    iat: issuedAt,
    exp: issuedAt + TOKEN_LIFETIME_SECONDS,
    jti: crypto.randomUUID(),
  });
  const signingInput = `${header}.${payload}`;
  const key = crypto.createPrivateKey({ key: jwk, format: "jwk" });
  const signature = crypto.sign("RSA-SHA256", Buffer.from(signingInput), key);
  return `${signingInput}.${signature.toString("base64url")}`;
}

function decodePrivateJwk(encodedPrivateJwk) {
  let json;
  try {
    json = Buffer.from(encodedPrivateJwk, "base64").toString("utf8");
  } catch {
    throw new Error("maskinporten-jwk is not valid base64.");
  }

  let jwk;
  try {
    jwk = JSON.parse(json);
  } catch {
    throw new Error("maskinporten-jwk did not contain a JSON private JWK.");
  }

  const requiredFields = ["kty", "kid", "n", "e", "d", "p", "q", "dp", "dq", "qi"];
  const missing = requiredFields.filter((field) => !jwk[field]);
  if (missing.length > 0) {
    throw new Error(`maskinporten-jwk is missing fields: ${missing.join(", ")}.`);
  }

  return jwk;
}

function readKeyVaultSecret(vaultName, secretName) {
  let output;
  try {
    output = execFileSync(
      "az",
      [
        "keyvault",
        "secret",
        "show",
        "--vault-name",
        vaultName,
        "--name",
        secretName,
        "--query",
        "value",
        "-o",
        "tsv",
      ],
      { encoding: "utf8", stdio: ["ignore", "pipe", "pipe"] }
    );
  } catch (error) {
    if (error.code === "ENOENT") {
      throw new Error("Azure CLI (az) was not found on PATH.");
    }

    const detail = `${error.stderr || ""}`.trim();
    throw new Error(
      `Failed to read Key Vault secret '${secretName}' from '${vaultName}'.${detail ? ` ${detail}` : ""}`
    );
  }

  const value = output.trim();
  if (!value) {
    throw new Error(`Key Vault secret '${secretName}' in '${vaultName}' is empty.`);
  }

  return value;
}

async function requestToken(tokenEndpoint, assertion) {
  const response = await fetch(tokenEndpoint, {
    method: "POST",
    redirect: "error",
    signal: AbortSignal.timeout(20000),
    headers: { "Content-Type": "application/x-www-form-urlencoded" },
    body: new URLSearchParams({
      grant_type: "urn:ietf:params:oauth:grant-type:jwt-bearer",
      assertion,
    }),
  });

  const rawBody = await response.text();
  let payload = {};
  if (rawBody) {
    try {
      payload = JSON.parse(rawBody);
    } catch {
      return { ok: false, status: response.status, error: "non-json-response", errorDescription: "" };
    }
  }

  return {
    ok: response.ok,
    status: response.status,
    accessToken: payload.access_token || "",
    error: payload.error || "",
    errorDescription: payload.error_description || "",
  };
}

function readAppsettings(filePath) {
  const contents = fs.readFileSync(filePath, "utf8").replace(/^\uFEFF/, "");
  return JSON.parse(contents);
}

function pickSetting(environmentSettings, baseSettings, key) {
  if (Object.prototype.hasOwnProperty.call(environmentSettings, key)) {
    return environmentSettings[key] ?? "";
  }

  return baseSettings[key] ?? "";
}

function isProduction(maskinportenEnvironment) {
  return maskinportenEnvironment.toLowerCase() === "prod" || maskinportenEnvironment.toLowerCase() === "production";
}

function base64UrlJson(value) {
  return Buffer.from(JSON.stringify(value)).toString("base64url");
}

function requiredEnv(name) {
  const value = (process.env[name] || "").trim();
  if (!value) {
    throw new Error(`${name} is required.`);
  }

  return value;
}

function mask(value) {
  if (value && process.env.GITHUB_ACTIONS === "true") {
    process.stdout.write(`::add-mask::${value}\n`);
  }
}

if (require.main === module) {
  main().catch((error) => {
    console.error(error instanceof Error ? error.message : String(error));
    process.exit(1);
  });
}

module.exports = {
  loadMaskinportenSettings,
  createClientAssertion,
  decodePrivateJwk,
};
