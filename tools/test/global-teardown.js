module.exports = async () => {
  await globalThis.__EMBEDDED_PG__?.stop();
};
