/**
 * Load test for creating correspondence
 * This test will create correspondence for a random end user and a random service owner
 * 
 */
import http from 'k6/http';
import exec from 'k6/execution';
import { 
    describe,
    uuidv4,
    expect,
    getEnterpriseToken
 } from "../common/testimports.js";
import { baseUrlCorrespondence, buildOptions } from '../common/config.js';
import { serviceOwners } from "../common/readTestdata.js";
import { getCorrespondenceForm } from '../data/correspondence-form.js';

const uploadCorrespondenceLabel = 'upload correspondence';
const labels = [uploadCorrespondenceLabel];

export { setup as setup } from "../common/readTestdata.js";

export let options = buildOptions(labels);

const traceCalls = (__ENV.traceCalls ?? 'false') === 'true';

export default function(data) {
    const myEndUsers = data[exec.vu.idInTest - 1];
    const ix = exec.vu.iterationInInstance % myEndUsers.length;
    var soIx = 0 //exec.vu.iterationInInstance%serviceOwners.length;
    uploadCorrespondence(serviceOwners[soIx], myEndUsers[ix], traceCalls); 
}

export function uploadCorrespondence(serviceOwner, endUser, traceCalls) {
    var traceparent = uuidv4();
    const boundary = '----WebKitFormBoundary' + Math.random().toString(36).substring(2);
    const formData = getCorrespondenceForm(serviceOwner.resource, serviceOwner.orgno, endUser.ssn, boundary);
    const tokenOptions = {
        scopes: serviceOwner.scopes, 
        orgno: serviceOwner.orgno,
        consumerOrgNo: serviceOwner.orgno,
        org: serviceOwner.org
    };
    var paramsWithToken = {
        headers: {
            Authorization: "Bearer " + getEnterpriseToken(tokenOptions),
            traceparent: traceparent,
            'Content-Type': 'multipart/form-data; boundary=' + boundary,
            'Accept': '*/*, application/json',
            'Accept-Encoding': 'gzip, deflate, br',
            'Connection': 'keep-alive'
        },
        tags: { name: uploadCorrespondenceLabel }
    };

    if (traceCalls) {
        paramsWithToken.tags.traceparent = traceparent;
        paramsWithToken.tags.enduser = endUser.ssn;
    }

    describe('upload correspondence', () => {
        let r = http.post(baseUrlCorrespondence + 'upload', formData, paramsWithToken);
        expect(r.status, 'response status').to.be.oneOf([200, 422]);
    });
}
