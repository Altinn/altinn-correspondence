/**
 * Load test for creating correspondence
 * This test will create correspondence for a random end user and a random service owner
 * 
 */
import http from 'k6/http';
import exec from 'k6/execution';
import { randomItem } from 'https://jslib.k6.io/k6-utils/1.2.0/index.js';
import { URL } from 'https://jslib.k6.io/url/1.0.0/index.js';
import { describe } from "../common/describe.js";
import { expect } from "../common/testimports.js";
import { serviceOwners } from "../common/readTestdata.js";
import { getPersonalTokenForEndUser, getDialogPortenToken } from '../common/token.js';
import { baseUrlCorrespondence } from '../common/config.js';
import { uuidv7 } from '../common/uuid.js';
export { setup as setup } from "../common/readTestdata.js";

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

export default function(data) {
    const myEndUsers = data[exec.vu.idInTest - 1];
    const ix = exec.vu.iterationInInstance % myEndUsers.length;
    getCorrespondence(serviceOwners[0], myEndUsers[ix], traceCalls);  
}

function getCorrespondence(serviceOwner, endUser, traceCalls) {
    var traceparent = uuidv7();

    var paramsWithToken = {
        headers: {
            Authorization: "Bearer " + getPersonalTokenForEndUser(serviceOwner, endUser),
            traceparent: traceparent,
            'Content-Type': 'application/json',
            'Accept': '*/*, application/json',
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
    if (correspondences.ids.length === 0) {
        console.log('No correspondence found for end user ' + endUser.ssn);
        return;
    }

    const listParams = {
        ...paramsWithToken,
        tags: { ...paramsWithToken.tags, name: 'get correspondence details' }
    };
    
    describe('get correspondence details', () => {
        let correspondenceId = correspondences.ids[0]; //randomItem(correspondences.items);
        let contentUrl = new URL(baseUrlCorrespondence + correspondenceId);
        let r = http.get(contentUrl.toString(), listParams);
        expect(r.status, 'response status').to.equal(200);
        getCorrespondenceContent(correspondences.ids, r, endUser, traceparent);
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