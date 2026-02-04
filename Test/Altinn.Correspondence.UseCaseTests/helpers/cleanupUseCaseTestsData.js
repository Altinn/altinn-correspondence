import { getMaintenanceMaskinportenToken } from './maskinportenTokenService.js';
import { check } from 'k6';
import http from 'k6/http';

const baseUrl = __ENV.base_url;

export async function cleanupBruksmonsterTestData(testRunId) {
	const token = await getMaintenanceMaskinportenToken();
	check(token, { 'Maintenance Maskinporten token obtained for cleanup': t => typeof t === 'string' && t.length > 0 });

	const headers = {
		Authorization: `Bearer ${token}`
	};

	const query = testRunId ? `?testRunId=${encodeURIComponent(testRunId)}` : '';
	const res = http.post(`${baseUrl}/correspondence/api/v1/maintenance/cleanup-bruksmonster${query}`, null, { headers });
	check(res, { 'Cleanup bruksmonster test data status 200': r => r.status === 200 });

	if (res.status === 200) {
        const body = res.json();
        console.log(`Cleanup summary: testRunId=${testRunId}, resourceId=${body.resourceId}, correspondencesFound=${body.correspondencesFound}, attachmentsFound=${body.attachmentsFound}, deleteDialogsJobId=${body.deleteDialogsJobId}, deleteCorrespondencesJobId=${body.deleteCorrespondencesJobId}`);
	} else {
		console.error(`Cleanup failed. Status: ${res.status}. Body: ${res.body}`);
	}
}
