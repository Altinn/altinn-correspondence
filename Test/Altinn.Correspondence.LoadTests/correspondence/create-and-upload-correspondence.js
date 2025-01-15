/**
 * Load test for creating correspondence
 * This test will create correspondence for a random end user and a random service owner
 * 
 */
import http from 'k6/http';
import exec from 'k6/execution';
import { randomItem } from 'https://jslib.k6.io/k6-utils/1.4.0/index.js';
import { describe } from "../common/describe.js";
import { baseUrlCorrespondence } from '../common/config.js';
import { expect } from "../common/testimports.js";
import { endUsers, serviceOwners } from "../common/readTestdata.js";
import { getCorrespondenceForm } from '../data/correspondence-form.js';
import { getPersonalTokenForServiceOwner } from '../common/token.js';
import { uuidv7 } from '../common/uuid.js';

export let options = {
    summaryTrendStats: ['avg', 'min', 'med', 'max', 'p(95)', 'p(99)', 'p(99.5)', 'p(99.9)', 'count'],
    thresholds: {   
        'http_req_duration{name:upload correspondence}': ['p(99)<5000'],
        'http_reqs{name:upload correspondence}': [],
        'checks{name:upload correspondence}': ['rate>0.95'],
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
    var ix = exec.vu.iterationInInstance%endUsers.length;
    var soIx = exec.vu.iterationInInstance%serviceOwners.length;
    //console.log("Iteration: " + exec.vu.iterationInInstance + ", endUser: " + ix + ", serviceOwner: " + soIx);
    uploadCorrespondence(serviceOwners[soIx], endUsers[ix], traceCalls);  
  }

  export function uploadCorrespondence(serviceOwner, endUser, traceCalls) {
    var traceparent = uuidv7();
    const formData = getCorrespondenceForm(serviceOwner.resource, serviceOwner.orgno, endUser.ssn);
    var paramsWithToken = {
        headers: {
            Authorization: "Bearer " + getPersonalTokenForServiceOwner(serviceOwner),
            traceparent: traceparent,
            'Content-Type': 'multipart/form-data; boundary=' + formData.boundary,
            'Accept': '*/*, text/plain',
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
        let r = await http.asyncRequest('POST', baseUrlCorrespondence + 'upload', formData.body(), paramsWithToken);
        expect(r.status, 'response status').to.equal(200);
    });
}
