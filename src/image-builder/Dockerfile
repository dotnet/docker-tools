# Use this Dockerfile to create a runner image
#     docker build -t image-builder .
#     docker run --rm -v /var/run/docker.sock:/var/run/docker.sock -v <local path to build>:/repo -w /repo image-builder <image-build args>

FROM microsoft/dotnet:1.1-sdk

# Install Docker
RUN apt-get update \
    && apt-get install -y --no-install-recommends \
        apt-transport-https \
        software-properties-common \
    && rm -rf /var/lib/apt/lists/*

RUN curl -fsSL https://download.docker.com/linux/debian/gpg | apt-key add - \
    && add-apt-repository "deb [arch=amd64] https://download.docker.com/linux/debian $(lsb_release -cs) stable" \
    && apt-get update \
    && apt-get install -y --no-install-recommends \
        docker-ce \
    && rm -rf /var/lib/apt/lists/*

# Install Go
RUN apt-get update && apt-get install -y --no-install-recommends \
        g++ \
        gcc \
        libc6-dev \
        make \
        pkg-config \
    && rm -rf /var/lib/apt/lists/*

ENV GOLANG_VERSION 1.8.1
ENV GOLANG_DOWNLOAD_URL https://golang.org/dl/go$GOLANG_VERSION.linux-amd64.tar.gz
ENV GOLANG_DOWNLOAD_SHA256 a579ab19d5237e263254f1eac5352efcf1d70b9dacadb6d6bb12b0911ede8994

RUN curl -fsSL "$GOLANG_DOWNLOAD_URL" -o golang.tar.gz \
    && echo "$GOLANG_DOWNLOAD_SHA256  golang.tar.gz" | sha256sum -c - \
    && tar -C /usr/local -xzf golang.tar.gz \
    && rm golang.tar.gz

ENV GOPATH /go
ENV PATH $GOPATH/bin:/usr/local/go/bin:$PATH

RUN mkdir -p "$GOPATH/src" "$GOPATH/bin" && chmod -R 777 "$GOPATH"
WORKDIR $GOPATH

# Install manifest-tool
RUN cd $GOPATH/src \
    && mkdir -p github.com/estesp \
    && cd github.com/estesp \
    && git clone https://github.com/estesp/manifest-tool \
    && cd manifest-tool \
    && make binary \
    && make install

# Build image-builder
WORKDIR image-builder
COPY *.csproj .
RUN dotnet restore
COPY . .
RUN dotnet publish -c Release -o /out

ENTRYPOINT ["dotnet", "/out/image-builder.dll"]
