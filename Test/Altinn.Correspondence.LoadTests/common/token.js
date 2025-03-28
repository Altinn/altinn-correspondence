import http from "k6/http";
import encoding from "k6/encoding";
import { tokenGeneratorEnv } from "./config.js";
import { baseUrlDialogPortenEndUser } from "./config.js";
import { expect } from "./testimports.js";

const tokenUsername = __ENV.TOKEN_GENERATOR_USERNAME;
const tokenPassword = __ENV.TOKEN_GENERATOR_PASSWORD;

const tokenTtl = parseInt(__ENV.TTL) || 3600;
const tokenMargin = 10;

const credentials = `${tokenUsername}:${tokenPassword}`;
const encodedCredentials = encoding.b64encode(credentials);
const tokenRequestOptions = {
  headers: {
    Authorization: `Basic ${encodedCredentials}`,
  },
};

let cachedTokens = {};
let cachedTokensIssuedAt = {};

function getCacheKey(tokenType, tokenOptions) {
  return `${tokenType}|${tokenOptions.scopes}|${tokenOptions.orgName}|${tokenOptions.orgNo}|${tokenOptions.ssn}`;
}

export function fetchToken(url, tokenOptions, type) {
  const currentTime = Math.floor(Date.now() / 1000);  
  const cacheKey = getCacheKey(type, tokenOptions);

  if (!cachedTokens[cacheKey] || (currentTime - cachedTokensIssuedAt[cacheKey] >= tokenTtl - tokenMargin)) {
    if (__VU == 0) {
      console.info(`Fetching ${type} token from token generator during setup stage`);
    }
    else {
      console.info(`Fetching ${type} token from token generator during VU stage for VU #${__VU}`);
    }
    
    let response = http.get(url, tokenRequestOptions);

    if (response.status != 200) {
        console.log(url);
        throw new Error(`Failed getting ${type} token: ${response.status_text}`);
    }
    cachedTokens[cacheKey] = response.body;
    cachedTokensIssuedAt[cacheKey] = currentTime;
  }

  return cachedTokens[cacheKey];
}

export function getEnterpriseToken(serviceOwner) {  
    const tokenOptions = {
        scopes: serviceOwner.scopes, 
        orgName: serviceOwner.org,
        orgNo: serviceOwner.orgno
    }
    const url = `https://altinn-testtools-token-generator.azurewebsites.net/api/GetEnterpriseToken?env=${tokenGeneratorEnv}&scopes=${encodeURIComponent(tokenOptions.scopes)}&org=${tokenOptions.orgName}&orgNo=${tokenOptions.orgNo}&ttl=${tokenTtl}`;
    return fetchToken(url, tokenOptions, `service owner (orgno:${tokenOptions.orgNo} orgName:${tokenOptions.orgName} tokenGeneratorEnv:${tokenGeneratorEnv})`);
}

export function getPersonalTokenForServiceOwner(serviceOwner) {
    const tokenOptions = {
        scopes: serviceOwner.scopes, 
        ssn: serviceOwner.ssn,
        orgno: serviceOwner.orgno
    }
    const url = `https://altinn-testtools-token-generator.azurewebsites.net/api/GetPersonalToken?env=${tokenGeneratorEnv}&scopes=${encodeURIComponent(tokenOptions.scopes)}&pid=${tokenOptions.ssn}&orgNo=${tokenOptions.orgno}&consumerOrgNo=${tokenOptions.orgno}&ttl=${tokenTtl}`;
    return fetchToken(url, tokenOptions, `end user (ssn:${tokenOptions.ssn}, tokenGeneratorEnv:${tokenGeneratorEnv})`);
  }

  export function getPersonalTokenForEndUser(serviceOwner, endUser) {
    const tokenOptions = {
        scopes: endUser.scopes, 
        ssn: endUser.ssn,
        orgno: serviceOwner.orgno
    }
    const url = `https://altinn-testtools-token-generator.azurewebsites.net/api/GetPersonalToken?env=${tokenGeneratorEnv}&scopes=${encodeURIComponent(tokenOptions.scopes)}&pid=${tokenOptions.ssn}&orgNo=${tokenOptions.orgno}&consumerOrgNo=${tokenOptions.orgno}&ttl=${tokenTtl}`;
    return fetchToken(url, tokenOptions, `end user (ssn:${tokenOptions.ssn}, tokenGeneratorEnv:${tokenGeneratorEnv})`);
  }

  export function getPersonalToken(endUser) {
    const tokenOptions = {
        scopes: endUser.scopes, 
        ssn: endUser.ssn
    }
    const url = `https://altinn-testtools-token-generator.azurewebsites.net/api/GetPersonalToken?env=${tokenGeneratorEnv}&scopes=${encodeURIComponent(tokenOptions.scopes)}&pid=${tokenOptions.ssn}&ttl=${tokenTtl}`;
    return fetchToken(url, tokenOptions, `end user (ssn:${tokenOptions.ssn}, tokenGeneratorEnv:${tokenGeneratorEnv})`);
  }

  export function getDialogPortenToken(endUser, detailsResponse, traceparent) {
    let correspondencesDetails = JSON.parse(detailsResponse.body);
    let dialogPortenId = correspondencesDetails.externalReferences.find(ref => ref.referenceType === 'DialogportenDialogId');
    const tokenOptions = {
        scopes: 'digdir:dialogporten', 
        ssn: endUser.ssn
    }
    let paramsWithToken = {
        headers: {
            Authorization: "Bearer " + getPersonalToken(tokenOptions),
            traceparent: traceparent
        },
        tags: { name: 'enduser search' } 
    }
    const dpUrl = baseUrlDialogPortenEndUser + 'dialogs/' + dialogPortenId.referenceValue;
    let r = http.get(dpUrl, paramsWithToken);
    expect(r.status, 'response status').to.equal(200);
    const dialogDetails = JSON.parse(r.body);
    return dialogDetails.dialogToken;
  }

  
  