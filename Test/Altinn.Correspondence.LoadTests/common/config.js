const testBaseUrl = "https://altinn-dev-api.azure-api.net/correspondence/";
const yt01BaseUrl = "https://platform.yt01.altinn.cloud/correspondence/";
const testDialogPortenBaseUrl = "https://altinn-dev-api.azure-api.net/dialogporten/";
const yt01DialogPortenBaseUrl = "https://platform.yt01.altinn.cloud/dialogporten/";

const endUserPath = "api/v1/correspondence/";
const dialogPortenEndUserPath = "api/v1/enduser/";

export const baseUrls = {
    v1: {
        correspondence: {
            test: testBaseUrl + endUserPath,
            yt01: yt01BaseUrl + endUserPath
        },

        dialogPortenEndUser: {
            test: testDialogPortenBaseUrl + dialogPortenEndUserPath,
            yt01: yt01DialogPortenBaseUrl + dialogPortenEndUserPath
        }

    }
};

if (__ENV.IS_DOCKER && __ENV.API_ENVIRONMENT == "localdev") {
    __ENV.API_ENVIRONMENT = "localdev_docker";
}

if (!baseUrls[__ENV.API_VERSION]) {
    throw new Error(`Invalid API version: ${__ENV.API_VERSION}. Please ensure it's set correctly in your environment variables.`);
}

if (!baseUrls[__ENV.API_VERSION]["correspondence"][__ENV.API_ENVIRONMENT]) {
    throw new Error(`Invalid enduser API environment: ${__ENV.API_ENVIRONMENT}. Please ensure it's set correctly in your environment variables.`);
}

export const baseUrlCorrespondence = baseUrls[__ENV.API_VERSION]["correspondence"][__ENV.API_ENVIRONMENT];
export const baseUrlDialogPortenEndUser = baseUrls[__ENV.API_VERSION]["dialogPortenEndUser"][__ENV.API_ENVIRONMENT];
export const tokenGeneratorEnv = __ENV.API_ENVIRONMENT == "yt01" ? "yt01" : "tt02"; // yt01 is the only environment that has a separate token generator environment

const tokenGenLabel = "Token generator";
export const breakpoint = __ENV.breakpoint;
export const stages_duration = (__ENV.stages_duration ?? '1m');
export const stages_target = (__ENV.stages_target ?? '5');
export const abort_on_fail = (__ENV.abort_on_fail ?? 'false') === 'true';

export function buildOptions(mylabels) {
    let options = {
        summaryTrendStats: ['avg', 'min', 'med', 'max', 'p(95)', 'p(99)', 'count'],
        thresholds: {
            checks: ['rate>=1.0'],
            [`http_req_duration{name:${tokenGenLabel}}`]: [],
            [`http_req_failed{name:${tokenGenLabel}}`]: ['rate<=0.0']
        }
    };
    if (breakpoint) {
        for (var label of mylabels) {
            options.thresholds[[`http_req_duration{name:${label}}`]] = [{ threshold: "max<5000", abortOnFail: abort_on_fail }];
            options.thresholds[[`http_req_failed{name:${label}}`]] = [{ threshold: 'rate<=0.0', abortOnFail: abort_on_fail }];
        }
        //options.executor = 'ramping-arrival-rate';
        options.stages = [
            { duration: stages_duration, target: stages_target },
        ];
    }
    else {
        for (var label of mylabels) {
            options.thresholds[[`http_req_duration{name:${label}}`]] = [];
            options.thresholds[[`http_req_failed{name:${label}}`]] = ['rate<=0.0'];
        }
    }
    return options;
}
