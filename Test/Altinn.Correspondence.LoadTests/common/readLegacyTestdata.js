/**
 * This file contains the implementation of reading test data from CSV files.
 * The test data includes service owners, end users, and end users with tokens.
 * The data is read using the PapaParse library and stored in SharedArray variables.
 * 
 * @module readTestdata
 */

import papaparse from 'https://jslib.k6.io/papaparse/5.1.1/index.js';
import { SharedArray } from "k6/data";
import exec from 'k6/execution';
import { breakpoint, stages_target } from './config.js';

/**
 * Function to read the CSV file specified by the filename parameter.
 * @param {} filename 
 * @returns 
 */
function readCsv(filename) {
  try {
    return papaparse.parse(open(filename), { header: true, skipEmptyLines: true,  }).data;
  } catch (error) {
    console.log(`Error reading CSV file: ${error}`);
    return [];
  } 
}

if (!__ENV.API_ENVIRONMENT) {
  throw new Error('API_ENVIRONMENT must be set');
}
const filenameOrgs = `../data/stratified-sampling-of-recipients-users.csv`;


/**
 * SharedArray variable that stores the service owners data.
 * The data is parsed from the CSV file specified by the filenameServiceowners variable.
 * 
 * @name serviceOwners
 * @type {SharedArray}
 */
export const orgs = new SharedArray('orgs', function () {
  return readCsv(filenameOrgs);
});


export function orgsParts(totalVus, vuId) {
    const orgsLength = orgs.length;
    if (totalVus == 1) {
        return orgs.slice(0, orgsLength);
    }
    let usersPerVU = Math.floor(orgsLength / totalVus);
    let extras = orgsLength % totalVus;
    let ixStart = (vuId-1) * usersPerVU;
    if (vuId <= extras) {
        usersPerVU++;
        ixStart += vuId - 1;
    }
    else {
        ixStart += extras;
    }
    return orgs.slice(ixStart, ixStart + usersPerVU);
}

export function setup() {
  let totalVus = 1;
  if (breakpoint) {
      totalVus = stages_target;
  } else {
      totalVus = exec.test.options.scenarios.default.vus;
  }
  let parts = [];
  for (let i = 1; i <= totalVus; i++) {
      parts.push(orgsParts(totalVus, i));
  }
  return parts;
}


