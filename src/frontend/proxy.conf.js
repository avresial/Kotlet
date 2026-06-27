const apiUrl =
  process.env.services__api__https__0 ??
  process.env.services__api__http__0 ??
  'https://localhost:7001';

module.exports = {
  '/api': {
    target: apiUrl,
    secure: false,
    changeOrigin: true,
  },
};
