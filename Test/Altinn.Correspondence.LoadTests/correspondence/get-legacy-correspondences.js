import http from 'k6/http';
import exec from 'k6/execution';
import { URL, describe, expect, uuidv4, getPersonalToken, randomItem } from '../common/testimports.js';
import { baseUrlLegacyCorrespondence, buildOptions } from '../common/config.js';
export { setup as setup } from "../common/readLegacyTestdata.js";

const getLegacyCorrespondencesLabel = 'get legacy correspondences';
const labels = [ getLegacyCorrespondencesLabel ];

export const options = buildOptions(labels);

const traceCalls = (__ENV.traceCalls ?? 'false') === 'true';

export default function(data) {
    const myEndUsers = data[exec.vu.idInTest - 1];
    getLegacyCorrespondences(randomItem(myEndUsers), traceCalls);  
}

function getLegacyCorrespondences(endUser, traceCalls) {
    var traceparent = uuidv4();

    const tokenOptions = {
        scopes: "altinn:portal/enduser", 
        partyId: endUser.UserPartyId,
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
        tags: { name: getLegacyCorrespondencesLabel }
    };

    if (traceCalls) {
        paramsWithToken.tags.traceparent = traceparent;
        paramsWithToken.tags.enduser = endUser.ssn;
    }

    describe('get legacy correspondences', () => {
        let url = new URL(baseUrlLegacyCorrespondence);
        const payload = {
            "instanceOwnerPartyIdList": [
                endUser.UserPartyId
            ],
            "offset": 0,
            "limit": 1000,
            "includeActive": true,
            "includeArchived": false,
            "includeDeleted": false,
            "from": "2005-06-17T09:16:47.7846114Z",
            "to": "9999-12-31T22:59:59.9999999Z",
            "searchString": null,
            "language": "nb",
            "filterMigrated": false
        }
        let r = http.post(url.toString(), JSON.stringify(payload), paramsWithToken);
        expect(r.status, 'response status').to.equal(200);
    });
    
}