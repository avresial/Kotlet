const apiUrl = [
  process.env.services__api__https__0,
  process.env.services__api__http__0,
].find((url) => url?.trim())?.trim() ?? 'https://localhost:7001';

module.exports = {
  '/api': {
    target: apiUrl,
    secure: false, // Accept self-signed localhost certificates in development only.
    changeOrigin: true,
  },
};
