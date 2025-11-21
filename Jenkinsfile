// Jenkinsfile for FTM Backend (.NET 7)
// This pipeline builds, tests, dockerizes, and updates GitOps repo

pipeline {
    agent any
    
    environment {
        // ACR Configuration
        ACR_NAME = 'acrftmbackenddev'
        ACR_REGISTRY = "${ACR_NAME}.azurecr.io"
        IMAGE_NAME = 'ftm-backend'
        
        // Git Configuration
        GIT_REPO_APP = 'https://github.com/yourorg/ftm-backend.git'
        GIT_REPO_GITOPS = 'https://github.com/yourorg/ftm-gitops.git'
        GIT_BRANCH = 'main'
        
        // Build Configuration
        DOTNET_VERSION = '7.0'
        BUILD_CONFIG = 'Release'
        
        // Credentials (stored in Jenkins)
        ACR_CREDENTIALS = credentials('acr-credentials')
        GIT_CREDENTIALS = credentials('git-credentials')
        
        // Dynamic versioning
        IMAGE_TAG = "${env.BUILD_NUMBER}"
        GIT_COMMIT_SHORT = sh(
            script: "git rev-parse --short HEAD",
            returnStdout: true
        ).trim()
    }
    
    options {
        // Keep last 10 builds
        buildDiscarder(logRotator(numToKeepStr: '10'))
        // Timeout after 30 minutes
        timeout(time: 30, unit: 'MINUTES')
        // Add timestamps to console output
        timestamps()
    }
    
    stages {
        stage('🔍 Checkout') {
            steps {
                echo "Checking out ${GIT_BRANCH} branch..."
                checkout scm
                script {
                    env.GIT_COMMIT_MSG = sh(
                        script: 'git log -1 --pretty=%B',
                        returnStdout: true
                    ).trim()
                }
                echo "Git Commit: ${env.GIT_COMMIT_SHORT}"
                echo "Commit Message: ${env.GIT_COMMIT_MSG}"
            }
        }
        
        stage('🔧 Restore Dependencies') {
            steps {
                echo 'Restoring NuGet packages...'
                dir('FTM-BE') {
                    sh """
                        dotnet restore FTM.sln
                    """
                }
            }
        }
        
        stage('🏗️ Build') {
            steps {
                echo "Building project in ${BUILD_CONFIG} mode..."
                dir('FTM-BE') {
                    sh """
                        dotnet build FTM.sln \\
                            --configuration ${BUILD_CONFIG} \\
                            --no-restore
                    """
                }
            }
        }
        
        stage('🧪 Unit Tests') {
            steps {
                echo 'Running unit tests...'
                dir('FTM-BE/FTM.Tests') {
                    sh """
                        dotnet test \\
                            --configuration ${BUILD_CONFIG} \\
                            --no-build \\
                            --logger "trx;LogFileName=test-results.trx" \\
                            --collect:"XPlat Code Coverage"
                    """
                }
            }
            post {
                always {
                    // Publish test results
                    junit '**/test-results.trx'
                    // Publish code coverage
                    publishCoverage adapters: [
                        coberturaAdapter('**/coverage.cobertura.xml')
                    ]
                }
            }
        }
        
        stage('🔒 Security Scan') {
            steps {
                echo 'Running security scan...'
                dir('FTM-BE') {
                    sh """
                        # Check for known vulnerabilities in NuGet packages
                        dotnet list package --vulnerable --include-transitive || true
                    """
                }
            }
        }
        
        stage('🐳 Docker Build') {
            steps {
                echo "Building Docker image: ${ACR_REGISTRY}/${IMAGE_NAME}:${IMAGE_TAG}"
                dir('FTM-BE') {
                    script {
                        docker.build(
                            "${ACR_REGISTRY}/${IMAGE_NAME}:${IMAGE_TAG}",
                            "--build-arg BUILD_CONFIG=${BUILD_CONFIG} ."
                        )
                        // Also tag as latest
                        sh """
                            docker tag ${ACR_REGISTRY}/${IMAGE_NAME}:${IMAGE_TAG} \\
                                       ${ACR_REGISTRY}/${IMAGE_NAME}:latest
                        """
                    }
                }
            }
        }
        
        stage('🔐 Login to ACR') {
            steps {
                echo 'Logging into Azure Container Registry...'
                sh """
                    echo ${ACR_CREDENTIALS_PSW} | docker login ${ACR_REGISTRY} \\
                        --username ${ACR_CREDENTIALS_USR} \\
                        --password-stdin
                """
            }
        }
        
        stage('📤 Push to ACR') {
            steps {
                echo 'Pushing Docker images to ACR...'
                sh """
                    docker push ${ACR_REGISTRY}/${IMAGE_NAME}:${IMAGE_TAG}
                    docker push ${ACR_REGISTRY}/${IMAGE_NAME}:latest
                """
            }
        }
        
        stage('📝 Update GitOps Repo') {
            when {
                // Only update GitOps for main branch
                branch 'main'
            }
            steps {
                echo 'Updating GitOps repository with new image tag...'
                script {
                    dir('gitops-repo') {
                        // Clone GitOps repo
                        git url: env.GIT_REPO_GITOPS,
                            branch: 'main',
                            credentialsId: 'git-credentials'
                        
                        // Update dev environment
                        sh """
                            cd overlays/dev
                            
                            # Update image tag using kustomize
                            kustomize edit set image \\
                                ${ACR_REGISTRY}/${IMAGE_NAME}:${IMAGE_TAG}
                            
                            # Commit changes
                            git config user.name "Jenkins CI"
                            git config user.email "jenkins@longops.io.vn"
                            git add kustomization.yaml
                            git commit -m "Update backend image to ${IMAGE_TAG} (build ${BUILD_NUMBER})" || true
                            
                            # Push changes
                            git push origin main
                        """
                        
                        echo "✅ GitOps repo updated with image tag: ${IMAGE_TAG}"
                    }
                }
            }
        }
        
        stage('🚀 Trigger ArgoCD Sync') {
            when {
                branch 'main'
            }
            steps {
                echo 'Triggering ArgoCD sync...'
                sh """
                    # Optional: Trigger ArgoCD sync via API
                    # argocd app sync ftm-backend-dev --prune
                    echo "ArgoCD will auto-sync in 3 minutes (or trigger manually)"
                """
            }
        }
    }
    
    post {
        success {
            echo '✅ Pipeline completed successfully!'
            // Send Slack notification
            slackSend(
                color: 'good',
                message: """
                    ✅ Backend Build SUCCESS
                    Job: ${env.JOB_NAME}
                    Build: ${env.BUILD_NUMBER}
                    Image: ${ACR_REGISTRY}/${IMAGE_NAME}:${IMAGE_TAG}
                    Commit: ${env.GIT_COMMIT_SHORT}
                    Message: ${env.GIT_COMMIT_MSG}
                """.stripIndent()
            )
        }
        failure {
            echo '❌ Pipeline failed!'
            slackSend(
                color: 'danger',
                message: """
                    ❌ Backend Build FAILED
                    Job: ${env.JOB_NAME}
                    Build: ${env.BUILD_NUMBER}
                    Commit: ${env.GIT_COMMIT_SHORT}
                    Check: ${env.BUILD_URL}
                """.stripIndent()
            )
        }
        always {
            // Clean up Docker images to save disk space
            sh """
                docker rmi ${ACR_REGISTRY}/${IMAGE_NAME}:${IMAGE_TAG} || true
                docker rmi ${ACR_REGISTRY}/${IMAGE_NAME}:latest || true
            """
            // Clean workspace
            cleanWs()
        }
    }
}
