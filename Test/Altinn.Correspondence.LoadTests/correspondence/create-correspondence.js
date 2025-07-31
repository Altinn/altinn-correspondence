/**
 * Load test for creating correspondence
 * This test will create correspondence for a random end user and a random service owner
 * 
 */
import http from 'k6/http';
import exec from 'k6/execution';
import { expect, describe, getPersonalToken, uuidv4 } from "../common/testimports.js";
import { serviceOwners } from "../common/readTestdata.js";
import { getCorrespondenceJson } from '../data/correspondence-json.js';
import { baseUrlCorrespondence, buildOptions } from '../common/config.js';
export { setup as setup } from "../common/readTestdata.js";

const createCorrespondenceLabel = 'create correspondence';
const labels = [createCorrespondenceLabel];


export let options = buildOptions(labels);

const traceCalls = (__ENV.traceCalls ?? 'false') === 'true';

export default function(data) {
    const myEndUsers = data[exec.vu.idInTest - 1];
    const ix = exec.vu.iterationInInstance % myEndUsers.length;
    var soIx = 0 //exec.vu.iterationInInstance%serviceOwners.length;
    createCorrespondence(serviceOwners[soIx], myEndUsers[ix], traceCalls); 
  }

function createCorrespondence(serviceOwner, endUser, traceCalls) {
    var traceparent = uuidv4();

    const tokenOptions = {
        scopes: serviceOwner.scopes, 
        pid: serviceOwner.ssn,
        orgno: serviceOwner.orgno,
        consumerOrgNo: serviceOwner.orgno
    };

    var paramsWithToken = {
        headers: {
            Authorization: "Bearer " + getPersonalToken(tokenOptions),
            traceparent: traceparent,
            'Content-Type': 'application/json',
            'Accept': '*/*, application/json',
            'Accept-Encoding': 'gzip, deflate, br',
            'Connection': 'keep-alive'
        },
        tags: { name: createCorrespondenceLabel }
    };
    
    if (traceCalls) {
        paramsWithToken.tags.traceparent = traceparent;
        paramsWithToken.tags.enduser = endUser.ssn;
    }

    describe('create correspondence', () => {
        let r = http.post(baseUrlCorrespondence, getCorrespondenceJson(serviceOwner.resource, serviceOwner.orgno, endUser.ssn), paramsWithToken);
        expect(r.status, 'response status').to.be.oneOf([200, 422]);
    });
}
