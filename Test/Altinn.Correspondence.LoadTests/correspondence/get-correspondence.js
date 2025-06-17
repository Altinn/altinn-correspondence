import http from 'k6/http';
import exec from 'k6/execution';
import { URL, describe, expect, uuidv4, getPersonalToken  } from '../common/testimports.js';
import { serviceOwners } from "../common/readTestdata.js";
import { baseUrlCorrespondence, baseUrlDialogPortenEndUser, buildOptions } from '../common/config.js';
export { setup as setup } from "../common/readTestdata.js";

const getCorrespondenceLabel = 'get correspondence';
const getCorrespondenceDetailsLabel = 'get correspondence details';
const getCorrespondenceContentLabel = 'get correspondence content';
const endUserSearchLabel = 'enduser search';
const labels = [
    getCorrespondenceLabel,
    getCorrespondenceDetailsLabel,
    getCorrespondenceContentLabel,
    endUserSearchLabel
];

export const options = buildOptions(labels);

const traceCalls = (__ENV.traceCalls ?? 'false') === 'true';

export default function(data) {
    const myEndUsers = data[exec.vu.idInTest - 1];
    const ix = exec.vu.iterationInInstance % myEndUsers.length;
    getCorrespondence(serviceOwners[0], myEndUsers[ix], traceCalls);  
}

function getCorrespondence(serviceOwner, endUser, traceCalls) {
    var traceparent = uuidv4();

    const tokenOptions = {
        scopes: endUser.scopes, 
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
        tags: { ...paramsWithToken.tags, name: getCorrespondenceDetailsLabel }
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
        tags: { name: getCorrespondenceContentLabel }
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
        pid: endUser.ssn
    }
    let paramsWithToken = {
        headers: {
            Authorization: "Bearer " + getPersonalToken(tokenOptions),
            traceparent: traceparent
        },
        tags: { name: endUserSearchLabel } 
    }
    const dpUrl = baseUrlDialogPortenEndUser + 'dialogs/' + dialogPortenId.referenceValue;
    let r = http.get(dpUrl, paramsWithToken);
    expect(r.status, 'response status').to.equal(200);
    const dialogDetails = JSON.parse(r.body);
    return dialogDetails.dialogToken;
  }