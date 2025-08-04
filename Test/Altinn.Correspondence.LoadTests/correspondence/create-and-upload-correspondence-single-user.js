/**
 * Load test for creating correspondence
 * This test will create correspondence for a random end user and a random service owner
 * 
 */
import http from 'k6/http';
import { 
    describe,
    getPersonalToken,
    uuidv4,
    expect
 } from "../common/testimports.js";
import { baseUrlCorrespondence, buildOptions } from '../common/config.js';
import { endUsers, serviceOwners } from "../common/readTestdata.js";
import { getCorrespondenceForm } from '../data/correspondence-form.js';

const uploadCorrespondenceLabel = 'upload correspondence';
const labels = [uploadCorrespondenceLabel];

export let options = buildOptions(labels);

const traceCalls = (__ENV.traceCalls ?? 'false') === 'true';

export default function() {
    uploadCorrespondence(serviceOwners[0], endUsers[0], traceCalls); 
}

export function uploadCorrespondence(serviceOwner, endUser, traceCalls) {
    var traceparent = uuidv4();
    const boundary = '----WebKitFormBoundary' + Math.random().toString(36).substring(2);
    const formData = getCorrespondenceForm(serviceOwner.resource, serviceOwner.orgno, endUser.ssn, boundary);
    const tokenOptions = {
        scopes: serviceOwner.scopes, 
        pid: serviceOwner.ssn,
        orgno: serviceOwner.orgno,
        consumerOrgNo: serviceOwner.orgno
    }
    var paramsWithToken = {
        headers: {
            Authorization: "Bearer " + getPersonalToken(tokenOptions),
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

    describe('upload correspondence', async () => {
        let r = await http.asyncRequest('POST', baseUrlCorrespondence + 'upload', formData, paramsWithToken);
        expect(r.status, 'response status').to.be.oneOf([200, 422]);
    });
}

