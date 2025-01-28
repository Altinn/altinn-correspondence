/**
 * Load test for creating correspondence
 * This test will create correspondence for a random end user and a random service owner
 * 
 */
import http from 'k6/http';
import exec from 'k6/execution';
import { describe } from "../common/describe.js";
import { expect } from "../common/testimports.js";
import { serviceOwners } from "../common/readTestdata.js";
import { getCorrespondenceJson } from '../data/correspondence-json.js';
import { getPersonalToken } from '../common/token.js';
import { baseUrlCorrespondence } from '../common/config.js';
import { uuidv7 } from '../common/uuid.js';
export { setup as setup } from "../common/readTestdata.js";

export let options = {
    summaryTrendStats: ['avg', 'min', 'med', 'max', 'p(95)', 'p(99)', 'p(99.5)', 'p(99.9)', 'count'],
    thresholds: {   
        'http_req_duration{name:create correspondence}': ['p(99)<5000'],
        'http_reqs{name:create correspondence}': [],
        'checks{name:create correspondence}': ['rate>0.95'],
    }
};

const traceCalls = (__ENV.traceCalls ?? 'false') === 'true';

export default function(data) {
    const myEndUsers = data[exec.vu.idInTest - 1];
    const ix = exec.vu.iterationInInstance % myEndUsers.length;
    var soIx = 0 //exec.vu.iterationInInstance%serviceOwners.length;
    createCorrespondence(serviceOwners[soIx], myEndUsers[ix], traceCalls); 
  }

function createCorrespondence(serviceOwner, endUser, traceCalls) {
    var traceparent = uuidv7();

    var paramsWithToken = {
        headers: {
            Authorization: "Bearer " + getPersonalToken(serviceOwner),
            traceparent: traceparent,
            'Content-Type': 'application/json',
            'Accept': '*/*, text/plain',
            'Accept-Encoding': 'gzip, deflate, br',
            'Connection': 'keep-alive'
        },
        tags: { name: 'create correspondence'}
    };
    
    if (traceCalls) {
        paramsWithToken.tags.traceparent = traceparent;
        paramsWithToken.tags.enduser = endUser.ssn;
    }

    describe('create correspondence', () => {
        let r = http.post(baseUrlCorrespondence, getCorrespondenceJson(serviceOwner.resource, serviceOwner.orgno, endUser.ssn), paramsWithToken);
        console.log(r.body);
        expect(r.status, 'response status').to.equal(200);
    });
}
