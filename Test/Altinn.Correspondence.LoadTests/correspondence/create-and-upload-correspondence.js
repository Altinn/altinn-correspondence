/**
 * Load test for creating correspondence
 * This test will create correspondence for a random end user and a random service owner
 * 
 */
import http from 'k6/http';
import exec from 'k6/execution';
import { describe } from "../common/describe.js";
import { baseUrlCorrespondence } from '../common/config.js';
import { expect } from "../common/testimports.js";
import { serviceOwners } from "../common/readTestdata.js";
import { getCorrespondenceForm } from '../data/correspondence-form.js';
import { getPersonalTokenForServiceOwner } from '../common/token.js';
import { uuidv7 } from '../common/uuid.js';
export { setup as setup } from "../common/readTestdata.js";

export let options = {
    summaryTrendStats: ['avg', 'min', 'med', 'max', 'p(95)', 'p(99)', 'p(99.5)', 'p(99.9)', 'count'],
    thresholds: {   
        'http_req_duration{name:upload correspondence}': ['p(99)<5000'],
        'http_reqs{name:upload correspondence}': [],
        'checks{name:upload correspondence}': ['rate>0.95'],
    }
};

const traceCalls = (__ENV.traceCalls ?? 'false') === 'true';

export default function(data) {
    const myEndUsers = data[exec.vu.idInTest - 1];
    const ix = exec.vu.iterationInInstance % myEndUsers.length;
    var soIx = 0 //exec.vu.iterationInInstance%serviceOwners.length;
    uploadCorrespondence(serviceOwners[soIx], myEndUsers[ix], traceCalls); 
}

export function uploadCorrespondence(serviceOwner, endUser, traceCalls) {
    var traceparent = uuidv7();
    const boundary = '----WebKitFormBoundary' + Math.random().toString(36).substring(2);
    const formData = getCorrespondenceForm(serviceOwner.resource, serviceOwner.orgno, endUser.ssn, boundary);
    var paramsWithToken = {
        headers: {
            Authorization: "Bearer " + getPersonalTokenForServiceOwner(serviceOwner),
            traceparent: traceparent,
            'Content-Type': 'multipart/form-data; boundary=' + boundary,
            'Accept': '*/*, application/json',
            'Accept-Encoding': 'gzip, deflate, br',
            'Connection': 'keep-alive'
        },
        tags: { name: 'upload correspondence'}
    };

    if (traceCalls) {
        paramsWithToken.tags.traceparent = traceparent;
        paramsWithToken.tags.enduser = endUser.ssn;
    }

    describe('upload correspondence', async () => {
        let r = await http.asyncRequest('POST', baseUrlCorrespondence + 'upload', formData, paramsWithToken);
        expect(r.status, 'response status').to.be.oneOf([200, 422]);
    });
}
