/**
 * Load test for creating correspondence
 * This test will create correspondence for a random end user and a random service owner
 * 
 */
import http from 'k6/http';
import exec from 'k6/execution';
import { URL } from 'https://jslib.k6.io/url/1.0.0/index.js';
import { randomItem } from 'https://jslib.k6.io/k6-utils/1.4.0/index.js';
import { describe } from "../common/describe.js";
import { expect } from "../common/testimports.js";
import { endUsers, serviceOwners } from "../common/readTestdata.js";
import { getPersonalTokenForEndUser, getPersonalToken } from '../common/token.js';
import { baseUrlCorrespondence, baseUrlDialogPortenEndUser } from '../common/config.js';
import { uuidv7 } from '../common/uuid.js';

export let options = {
    summaryTrendStats: ['avg', 'min', 'med', 'max', 'p(95)', 'p(99)', 'p(99.5)', 'p(99.9)', 'count'],
    thresholds: {   
        'http_req_duration{name:get correspondence}': ['p(99)<5000'],
        'http_reqs{name:get correspondence}': [],
        'checks{name:get correspondence}': ['rate>0.95'],
        'http_req_duration{name:get correspondence details}': ['p(99)<5000'],
        'http_reqs{name:get correspondence details}': [],
        'checks{name:get correspondence details}': [],
        'http_req_duration{name:get correspondence content}': ['p(99)<5000'],
        'http_reqs{name:get correspondence content}': [],
        'checks{name:get correspondence content}': [],
        'http_req_duration{name:enduser search}': [],
        'http_reqs{name:enduser search}': [],
    }
};

const traceCalls = (__ENV.traceCalls ?? 'false') === 'true';

export default function() {
    if (!endUsers || endUsers.length === 0) {
        throw new Error('No end users loaded for testing');
    }
    if (!serviceOwners || serviceOwners.length === 0) {
        throw new Error('No service owners loaded for testing');
    } 
    getCorrespondence(serviceOwners[getIx(serviceOwners.length)], endUsers[getIx(endUsers.length)], traceCalls);  
  }

function getIx(length) {
    if (length < exec.instance.vusActive) {
        return exec.vu.iterationInInstance%length;
    }
    let usersPerVU = Math.floor(length / exec.instance.vusActive);
    let extras = length % exec.instance.vusActive;
    let ixStart = (exec.vu.idInTest-1) * usersPerVU;
    if (exec.vu.idInTest <= extras) {
        usersPerVU++;
        if (exec.vu.idInTest > 1) {
            ixStart += exec.vu.idInTest;
        }
    }
    else {
        ixStart += extras;
    }
    const ix = ixStart + exec.vu.iterationInInstance%usersPerVU;
    return ix
}

function getCorrespondence(serviceOwner, endUser, traceCalls) {
    var traceparent = uuidv7();

    var paramsWithToken = {
        headers: {
            Authorization: "Bearer " + getPersonalTokenForEndUser(serviceOwner, endUser),
            traceparent: traceparent,
            'Content-Type': 'application/json',
            'Accept': '*/*, text/plain',
            'Accept-Encoding': 'gzip, deflate, br',
            'Connection': 'keep-alive'
        },
        tags: { name: 'get correspondence'}
    };

    if (traceCalls) {
        paramsWithToken.tags.traceparent = traceparent;
        paramsWithToken.tags.enduser = endUser.ssn;
    }

    describe('get correspondence', () => {
        let url = new URL(baseUrlCorrespondence);
        url.searchParams.append('resourceId', serviceOwner.resource);
        url.searchParams.append('role', 'Recipient');
        url.searchParams.append('onBehalfOf', endUser.ssn);
        let r = http.get(url.toString(), paramsWithToken);
        expect(r.status, 'response status').to.equal(200);
        getContent(r, endUser, traceparent, paramsWithToken);
    });
    
}

function getContent(response, endUser, traceparent, paramsWithToken) {
    let correspondences = JSON.parse(response.body);
    if (correspondences.items.length === 0) {
        console.log('No correspondence found for end user ' + endUser.ssn);
        return;
    }

    const listParams = {
        ...paramsWithToken,
        tags: { ...paramsWithToken.tags, name: 'get correspondence details' }
    };
    
    describe('get correspondence details', () => {
        let correspondenceId = randomItem(correspondences.items);
        let contentUrl = new URL(baseUrlCorrespondence + correspondenceId);
        let r = http.get(contentUrl.toString(), listParams);
        expect(r.status, 'response status').to.equal(200);
        getCorrespondenceContent(correspondences.items, r, endUser, traceparent);
    });
}

function getCorrespondenceContent(correspondenceIds, detailsResponse, endUser, traceparent) {
    var params = {
        headers: {
            Authorization: "Bearer " + getDialogPortenToken(endUser, detailsResponse, traceparent),
            traceparent: traceparent,
            'Content-Type': 'application/json',
            'Accept': '*/*, text/plain',
            'Accept-Encoding': 'gzip, deflate, br',
            'Connection': 'keep-alive'
        },
        tags: { name: 'get correspondence content'}
    };
    for (const correspondenceId of correspondenceIds) {
        describe('get correspondence content', () => {
            let contentUrl = new URL(baseUrlCorrespondence + correspondenceId + '/content');
            let r = http.get(contentUrl.toString(), params);
            expect(r.status, 'response status').to.equal(200);
        });
    }
}

function getDialogPortenToken(endUser, detailsResponse, traceparent) {
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
