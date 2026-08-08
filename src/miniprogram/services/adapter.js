const cloudAdapter = require("./cloudAdapter");
const mockAdapter = require("./mockAdapter");

function select(state) {
  return state.settings.mode === "cloud" ? cloudAdapter : mockAdapter;
}

module.exports = {
  select
};
