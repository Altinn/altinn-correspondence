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
