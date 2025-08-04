/**
 * Load test for creating correspondence
 * This test will create correspondence for a random end user and a random service owner
 * 
 */
import http from 'k6/http';
import exec from 'k6/execution';
import { URL, describe, expect, getPersonalToken, uuidv4 } from '../common/testimports.js';
import { serviceOwners } from "../common/readTestdata.js";
import { baseUrlCorrespondence, buildOptions } from '../common/config.js';
export { setup as setup } from "../common/readTestdata.js";

const getCorrespondenceLabel = 'get correspondence';
const getCorrespondenceDetailsLabel = 'get correspondence details';
const labels = [getCorrespondenceLabel, getCorrespondenceDetailsLabel];

export let options = buildOptions(labels);

const traceCalls = (__ENV.traceCalls ?? 'false') === 'true';

export default function(data) {
    const myEndUsers = data[exec.vu.idInTest - 1];
    const ix = exec.vu.iterationInInstance % myEndUsers.length;

    getCorrespondence(serviceOwners[0], myEndUsers[ix], traceCalls);  
}

function getCorrespondence(serviceOwner, endUser, traceCalls) {
    var traceparent = uuidv4();

    const tokenOptions = {
        scopes: "altinn:correspondence.read", 
        pid: endUser.ssn,
        orgno: serviceOwner.orgno,
        consumerOrgNo: serviceOwner.orgno
    }

    var paramsWithToken = {
        headers: {
            Authorization: "Bearer " + getPersonalToken(tokenOptions),
            traceparent: traceparent,
            'Content-Type': 'application/json',
            'Accept': '*/*, application/json',
            'Accept-Encoding': 'gzip, deflate, br',
            'Connection': 'keep-alive'
        },
        tags: { name: getCorrespondenceLabel }
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
        getOverview(r, endUser, paramsWithToken);
    });
    
}

function getOverview(response, endUser, paramsWithToken) {
    let correspondences = JSON.parse(response.body);
    if (correspondences.ids.length === 0) {
        console.log('No correspondence found for end user ' + endUser.ssn);
        return;
    }

    const listParams = {
        ...paramsWithToken,
        tags: { ...paramsWithToken.tags, name: getCorrespondenceDetailsLabel }
    };
    
    for (const correspondenceId of correspondences.ids) {
        describe('get correspondence details', () => {
            let contentUrl = new URL(baseUrlCorrespondence + correspondenceId);
            let r = http.get(contentUrl.toString(), listParams);
            expect(r.status, 'response status').to.equal(200);
        });
    }
}